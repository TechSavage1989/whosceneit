using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using WhoSceneIt.Deletion;
using WhoSceneIt.Retention;

public static class RetentionChecks
{
    public static async Task Run(string root, Action<string, Action> test, Func<string, Func<Task>, Task> testAsync, Action<bool, string> assert)
    {
        RetentionStore Store() => new(Path.Combine(root,"retention-"+Guid.NewGuid().ToString("N"),"retention.json"),new TestJson());
        void Reject(Action action) { try { action(); } catch(Exception e) when(e is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return; } throw new Exception("Unsafe retention input was accepted."); }
        async Task<DeletionStatus> Done(DeletionManager manager,string token) { var end=DateTimeOffset.UtcNow.AddSeconds(5); DeletionStatus status; do { await Task.Delay(10); status=manager.Status("admin",token); } while((status.State=="Deleting" || manager.IsRunning) && DateTimeOffset.UtcNow<end); return status; }
        test("Never-delete records persist, protect current and future show episodes, and respect path boundaries",()=>
        {
            var store=Store(); var show=new Series { InternalId=10,Name="Keep show",Path="/media/tv/Keep" }; var movie=new Movie { InternalId=20,Name="Keep film",Path="/media/film.mkv" };
            var library=Proxy.Create<ILibraryManager>((m,a)=>m.Name=="GetItemById"?(long)a[0]==10?show:movie:null);
            store.Protect(new[]{"10","20"},true,library);
            assert(store.Reason(new Episode { InternalId=30,SeriesId=10,Path="/other/episode.mkv" })!=null,"A future episode escaped inherited ID protection.");
            assert(store.Reason(new Episode { InternalId=31,Path="/media/tv/Keep/s1/a.mkv" })!=null,"A show path lost inherited protection.");
            assert(store.Reason(new Episode { InternalId=32,Path="/media/tv/Keep-other/a.mkv" })==null,"Protection crossed a path boundary.");
            assert(store.Reason(new Movie { InternalId=99,Path=movie.Path })!=null,"A reindexed movie lost exact-path protection.");
            var file=(string)typeof(RetentionStore).GetField("_file",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(store); var reloaded=new RetentionStore(file,new TestJson());
            assert(reloaded.Read().Protected.Length==2 && reloaded.Reason(movie)!=null,"Protection did not survive a server-state reload.");
            reloaded.Protect(new[]{"30"},false,library); assert(reloaded.Reason(new Episode { InternalId=30,SeriesId=10 })!=null,"Unprotecting an episode bypassed its protected show.");
            var view=reloaded.Read(); view.Protected[0].ItemId="invalid"; assert(reloaded.Read().Protected[0].ItemId=="10","A mutable API snapshot changed protection.");
        });
        await testAsync("Never delete blocks every native mode and protection added after preview is rechecked",async()=>
        {
            var store=Store();var f=new NativeDeletionFixture(root);var backend=new EmbyDeletionBackend(f.Users,f.Data,f.Library,f.Sessions,f.Host,store.Reason,store.LockProtection);var manager=new DeletionManager(backend);
            var preview=manager.Preview("admin","alice",new[]{"1"},true,true);store.Protect(new[]{"1"},true,f.Library);
            assert(backend.Inspect("admin","alice","1",true,true).Item.State=="Blocked","A broader watch-state mode bypassed never delete.");
            manager.Execute("admin",preview.Token,true);var done=await Done(manager,preview.Token);assert(done.Skipped==1 && f.NativeDeletes==0 && File.Exists(f.Movie.Path),"Late protection did not preserve native media.");
        });
        test("Watched-with-resume permission preserves genuinely unfinished progress and never changes flags",()=>
        {
            var f=new NativeDeletionFixture(root){Position=100,OtherPosition=200};
            assert(f.Backend.Inspect("admin","alice","1").Item.State=="Blocked" && f.Backend.Inspect("admin","alice","1",false,true).Item.State=="Ready","Completed resume mode was merged with ordinary deletion.");
            var other=f.Data;var data=Proxy.Create<IUserDataManager>((m,a)=>m.Name=="GetUserData"?new UserItemData{Played=((User)a[0]).InternalId==4,PlaybackPositionTicks=100}:null);
            var backend=new EmbyDeletionBackend(f.Users,data,f.Library,f.Sessions,f.Host,_=>null);
            assert(backend.Inspect("admin","alice","1",false,true).Item.State=="Blocked" && f.Position==100 && f.OtherPosition==200,"Another user's unfinished progress was deleted or changed.");
        });
        test("Native cleanup inventory uses completion, resume, age, and full-show state without inventing dates",()=>
        {
            var native=new NativeDeletionFixture(root);var now=DateTimeOffset.Parse("2026-10-25T12:00:00Z");var old=now.AddDays(-10);
            var items=new List<BaseItem>();var data=new Dictionary<long,UserItemData>();
            void Add(long id,bool played,long position,DateTimeOffset? date,long series=0) { var file=Path.Combine(root,"catalog-"+id+".mkv");File.WriteAllText(file,"fixture");items.Add(series==0?new Movie{InternalId=id,Path=file}:new Episode{InternalId=id,Path=file,SeriesId=series});data[id]=new UserItemData{Played=played,PlaybackPositionTicks=position,LastPlayedDate=date}; }
            Add(1,true,0,old);Add(2,true,100,old);Add(3,false,100,old);Add(4,true,0,null);Add(5,true,0,now);Add(11,true,0,old,10);Add(12,false,0,null,10);Add(13,true,0,old,20);Add(14,true,100,old,20);
            var library=Proxy.Create<ILibraryManager>((m,a)=>m.Name=="GetItemById"?items.SingleOrDefault(i=>i.InternalId==(long)a[0]):m.Name=="GetItemList"?items.Where(i=>{var q=(InternalItemsQuery)a[0];assert(q.AncestorIds!=null,"A cleanup query cleared SDK collection defaults.");return q.AncestorIds.Length>0?q.AncestorIds.Contains(i.SeriesId):q.IncludeItemTypes.Contains(i is Movie?"Movie":"Episode");}).ToArray():null);
            var source=Proxy.Create<IUserDataManager>((m,a)=>m.Name=="GetUserData"?data[((BaseItem)a[1]).InternalId]:null);var catalog=new EmbyCleanupCatalog(native.Users,source,library);var p=new CleanupPolicy{OwnerId="admin",UserId="alice",Scope="Movies"};
            assert(catalog.Candidates(p,now,null).SequenceEqual(new[]{"1"}),"Ordinary cleanup included resume, partial, recent, or unknown-age media.");p.WatchMode="Resume";assert(catalog.Candidates(p,now,null).SequenceEqual(new[]{"2"}),"Resume-only cleanup lost the completion requirement.");p.WatchMode="Both";p.MinimumDays=0;assert(catalog.Candidates(p,now,null).Length==4,"Zero-day policy fabricated completion or rejected manual watched marks.");p.Scope="CompletedShows";assert(catalog.Candidates(p,now,new[]{"13"}).SequenceEqual(new[]{"13","14"}),"A full-show event included an incomplete show or omitted its other completed episode.");
            Add(15,false,0,null,20);assert(catalog.Ineligible(p,"13",now)!=null,"A newly available unwatched episode did not stop full-show cleanup.");
        });
        test("Corrupt and unwritable retention state fail closed",()=>
        {
            var directory=Path.Combine(root,"broken-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var file=Path.Combine(directory,"retention.json");File.WriteAllText(file,"{broken");
            var bad=new RetentionStore(file,new TestJson());Reject(()=>bad.Read());Reject(()=>bad.Reason(new Movie{InternalId=1}));
            var parent=Path.Combine(root,"not-a-directory-"+Guid.NewGuid().ToString("N"));File.WriteAllText(parent,"fixture");var unwritable=new RetentionStore(Path.Combine(parent,"retention.json"),new TestJson());Reject(()=>unwritable.Change(s=>{}));Reject(()=>unwritable.Read());
        });
        test("UTC daily, weekly, and interval schedules advance strictly beyond the current instant",()=>
        {
            var now=DateTimeOffset.Parse("2026-10-25T03:00:00Z");var p=new CleanupPolicy{Enabled=true,Schedule="Daily",TimeUtc="03:00"};
            assert(CleanupSchedule.Next(p,now)==now.AddDays(1),"Daily schedule repeated the same occurrence.");p.Schedule="Weekly";p.Weekday=0;assert(CleanupSchedule.Next(p,now)==now.AddDays(7),"Weekly schedule repeated the same occurrence.");p.Schedule="Interval";p.IntervalHours=6;assert(CleanupSchedule.Next(p,now)==now.AddHours(6),"Interval schedule was wrong.");p.Enabled=false;assert(CleanupSchedule.Next(p,now)==null,"A disabled rule retained a schedule.");
        });
        test("Saved schema-1 rules retain every setting and active schedules after a fresh coordinator starts",()=>
        {
            var f=new CleanupFixture(root);var directory=Path.Combine(root,"legacy-rules-"+Guid.NewGuid().ToString("N"));RetentionCompatibilityFixture.Write(directory);
            var store=new RetentionStore(Path.Combine(directory,"retention.json"),new TestJson());RetentionCompatibilityFixture.AssertState(new TestJson(),store.Read());RetentionCompatibilityFixture.AssertFiles(directory);
            var coordinator=new CleanupCoordinator(store,f.Catalog,f.Manager,()=>f.Now);var status=coordinator.Status();
            assert(status.Settings.Policies.Length==3 && status.Message.Contains("2 enabled; 1 disabled") && status.Message.Contains("remain active"),"Restart reported existing enabled rules as disabled or reset the rule list.");
            var originalAuto=new TestJson().SerializeToString(store.Read().Policies.Single(p=>p.Id=="legacy-auto"));var originalDisabled=new TestJson().SerializeToString(store.Read().Policies.Single(p=>p.Id=="legacy-disabled"));
            coordinator.Tick();var state=store.Read();
            assert(state.Reviews.Any(r=>r.PolicyId=="legacy-review" && r.ItemIds.SequenceEqual(new[]{"1","2","3"})) && f.Backend.Deletes==0,"The existing due review schedule did not run, or it silently became automatic deletion.");
            assert(new TestJson().SerializeToString(state.Policies.Single(p=>p.Id=="legacy-auto"))==originalAuto && new TestJson().SerializeToString(state.Policies.Single(p=>p.Id=="legacy-disabled"))==originalDisabled,"Evaluating one restored schedule changed another saved rule.");
        });
        test("Unsupported schema and a missing primary retain existing rules and backups without resetting defaults",()=>
        {
            var directory=Path.Combine(root,"incompatible-rules-"+Guid.NewGuid().ToString("N"));RetentionCompatibilityFixture.Write(directory);var file=Path.Combine(directory,"retention.json");
            var incompatible=RetentionCompatibilityFixture.Read().Replace("\"SchemaVersion\": 1","\"SchemaVersion\": 99");File.WriteAllText(file,incompatible);
            var bad=new RetentionStore(file,new TestJson());Reject(()=>bad.Read());Reject(()=>bad.Change(s=>s.Policies=Array.Empty<CleanupPolicy>()));
            assert(File.ReadAllText(file)==incompatible && File.ReadAllText(file+".bak")==RetentionCompatibilityFixture.Read()+"\n","An unsupported schema overwrote saved rules or their backup.");
            File.Delete(file);var missing=new RetentionStore(file,new TestJson());Reject(()=>missing.Read());Reject(()=>missing.Change(s=>{}));
            assert(!File.Exists(file) && File.ReadAllText(file+".bak")==RetentionCompatibilityFixture.Read()+"\n","Missing primary state was silently replaced by empty rules or an obsolete automatic backup.");
        });
        test("Cleanup rules default to review and require fresh authorization for enabled automatic deletion",()=>
        {
            var f=new CleanupFixture(root);var p=f.Policy();assert(!p.Enabled && p.Action=="Review" && p.MinimumDays==7,"Defaults enabled unattended cleanup.");p.Enabled=true;p.Action="Automatic";Reject(()=>f.Coordinator.Save("admin",p,false));p=f.Coordinator.Save("admin",p,true);assert(p.OwnerId=="admin" && !string.IsNullOrEmpty(p.Revision),"The rule was not bound to the actual actor.");assert(f.Coordinator.Status().Message.Contains("saved and enabled"),"The status still reported disabled after enabling cleanup.");Reject(()=>f.Coordinator.Save("someone-else",p,true));
            p=f.Store.Read().Policies.Single();p.MaximumItems=501;Reject(()=>f.Coordinator.Save("admin",p,true));
        });
        await testAsync("On-demand evaluation queues a fresh checkbox review even for automatic rules",async()=>
        {
            var f=new CleanupFixture(root);var p=f.Policy();p.Enabled=true;p.Action="Automatic";p=f.Coordinator.Save("admin",p,true);f.Coordinator.EvaluateNow("admin",p.Id);
            assert(f.Backend.Deletes==0 && f.Store.Read().Reviews.Single().ItemIds.Length==3 && f.Store.Read().Holds.Length==0,"On demand started deletion or claimed an attempt too early.");
            var preview=f.Coordinator.OpenReview("admin",f.Store.Read().Reviews[0].Id);Reject(()=>f.Manager.Execute("admin",preview.Token,false));f.Manager.Execute("admin",preview.Token,true);var result=await Done(f.Manager,preview.Token);
            assert(result.Deleted==3 && f.Store.Read().Reviews.Length==0 && f.Store.Read().Runs.Any(r=>r.State=="Completed") && f.Store.Read().Holds.Length==0,"Reviewed cleanup did not finish or left incorrect holds.");assert(f.Coordinator.Status().Message.Contains("Deletion finished"),"Completed cleanup retained a stale started message.");
        });
        await testAsync("Scheduled automatic cleanup journals targets before deletion and uses completed resume mode only",async()=>
        {
            var f=new CleanupFixture(root);var p=f.Policy();p.Enabled=true;p.Action="Automatic";p.WatchMode="Both";p.Schedule="Interval";p.IntervalHours=1;p=f.Coordinator.Save("admin",p,true);f.Now=f.Now.AddHours(1);f.Backend.BeforeDelete=()=>assert(f.Store.Read().Holds.Length>0 && f.Store.Read().Runs.Any(r=>r.State=="Deleting"),"Media deletion began before durable attempt records.");
            f.Coordinator.Tick();var run=f.Store.Read().Runs.Single(r=>r.Token!=null);var done=await Done(f.Manager,run.Token);
            assert(done.Deleted==3 && !f.Backend.DeleteAllowUnwatched && f.Backend.DeleteAllowWatchedResume && f.Store.Read().Holds.Length==0,"Automatic mode allowed unfinished deletion or lost journal cleanup.");
            assert(CleanupSchedule.Parse(f.Store.Read().Policies[0].NextRunUtc)>f.Now,"The consumed schedule occurrence was replayable.");
        });
        test("Playback triggers are user-scoped, coalesced, delayed, and rate-limited",()=>
        {
            var f=new CleanupFixture(root);var p=f.Policy();p.Enabled=true;p.OnPlaybackStopped=true;p=f.Coordinator.Save("admin",p,false);
            f.Coordinator.SignalEvent("Playback stopped","bob","1");f.Now=f.Now.AddMinutes(1);f.Coordinator.Tick();assert(f.Store.Read().Reviews.Length==0,"Another user's event triggered the rule.");
            f.Coordinator.SignalEvent("Playback stopped","alice","1");f.Coordinator.SignalEvent("Playback stopped","alice","2");f.Coordinator.Tick();assert(f.Store.Read().Reviews.Length==0,"The event settling delay was bypassed.");f.Now=f.Now.AddSeconds(31);f.Coordinator.Tick();assert(f.Store.Read().Reviews.Single().ItemIds.SequenceEqual(new[]{"1","2"}),"Events were not coalesced or included unrelated media.");
            f.Coordinator.SignalEvent("Playback stopped","alice","3");f.Now=f.Now.AddSeconds(31);f.Coordinator.Tick();assert(f.Store.Read().Reviews.Single().ItemIds.Length==2,"The per-rule cooldown was bypassed.");f.Now=f.Now.AddMinutes(15);f.Coordinator.Tick();assert(f.Store.Read().Reviews.Single().ItemIds.SequenceEqual(new[]{"3"}),"The delayed event never ran.");
        });
        test("Rule edits invalidate old prompts and executable previews",()=>
        {
            var f=new CleanupFixture(root);var p=f.Coordinator.Save("admin",f.Policy(),false);f.Coordinator.EvaluateNow("admin",p.Id);var preview=f.Coordinator.OpenReview("admin",f.Store.Read().Reviews[0].Id);p.MinimumDays=30;f.Coordinator.Save("admin",p,false);Reject(()=>f.Manager.Execute("admin",preview.Token,true));assert(f.Backend.Deletes==0 && f.Store.Read().Reviews.Length==0,"An old rule preview executed after editing.");
        });
        test("An on-demand review does not postpone a saved schedule and failed journaling prevents deletion",()=>
        {
            var f=new CleanupFixture(root);var p=f.Policy();p.Enabled=true;p.Schedule="Interval";p=f.Coordinator.Save("admin",p,false);var next=p.NextRunUtc;f.Now=f.Now.AddHours(1);f.Coordinator.EvaluateNow("admin",p.Id);assert(f.Store.Read().Policies[0].NextRunUtc==next,"A review postponed scheduled cleanup.");
            var preview=f.Coordinator.OpenReview("admin",f.Store.Read().Reviews[0].Id);var file=(string)typeof(RetentionStore).GetField("_file",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.Store);var folder=Path.GetDirectoryName(file);Directory.Delete(folder,true);File.WriteAllText(folder,"fixture storage failure");Reject(()=>f.Manager.Execute("admin",preview.Token,true));assert(f.Backend.Deletes==0 && File.Exists(f.Backend.FileFor("1")),"Deletion started without durable attempt records.");
        });
        await testAsync("Disabling a running rule stops remaining items after the in-flight item",async()=>
        {
            var f=new CleanupFixture(root);using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();f.Backend.Entered=entered;f.Backend.Release=release;var p=f.Policy();p.Enabled=true;p.Action="Automatic";p.OnStartup=true;p=f.Coordinator.Save("admin",p,true);f.Coordinator.SignalEvent("Startup");f.Now=f.Now.AddMinutes(1);f.Coordinator.Tick();
            assert(await Task.Run(()=>entered.Wait(3000)),"Cleanup did not start.");var run=f.Store.Read().Runs.Single(r=>r.Token!=null);try { p.Enabled=false;f.Coordinator.Save("admin",p,false); } finally { release.Set(); }var done=await Done(f.Manager,run.Token);assert(done.Deleted==1 && done.Skipped==2 && File.Exists(f.Backend.FileFor("2")),"Disabling the rule deleted queued items.");
        });
        await testAsync("Fresh watch eligibility and revoked rule ownership prevent queued deletion",async()=>
        {
            var f=new CleanupFixture(root);var p=f.Coordinator.Save("admin",f.Policy(),false);f.Coordinator.EvaluateNow("admin",p.Id);var preview=f.Coordinator.OpenReview("admin",f.Store.Read().Reviews[0].Id);f.Catalog.Blocked=true;f.Manager.Execute("admin",preview.Token,true);var done=await Done(f.Manager,preview.Token);assert(done.Skipped==3 && f.Backend.Deletes==0,"Changed completion state was not rechecked.");
            var g=new CleanupFixture(root);var rule=g.Policy();rule.Enabled=true;rule.Action="Automatic";rule.OnStartup=true;g.Coordinator.Save("admin",rule,true);g.Catalog.OwnerRevoked=true;g.Coordinator.SignalEvent("Startup");g.Now=g.Now.AddMinutes(1);g.Coordinator.Tick();assert(g.Backend.Deletes==0 && g.Store.Read().Reviews.Length==0,"A revoked administrator's rule ran.");
        });
        await testAsync("Native uncertainty remains on durable automation hold and is never retried after restart",async()=>
        {
            var f=new CleanupFixture(root);f.Backend.FailAfterDelete="1";var p=f.Policy();p.Enabled=true;p.Action="Automatic";p.OnStartup=true;p=f.Coordinator.Save("admin",p,true);f.Coordinator.SignalEvent("Startup");f.Now=f.Now.AddMinutes(1);f.Coordinator.Tick();var run=f.Store.Read().Runs.Single(r=>r.Token!=null);await Done(f.Manager,run.Token);assert(f.Store.Read().Holds.Single().ItemId=="1","An uncertain native attempt lost its hold.");
            f.Store.Change(s=>{s.Runs[0].State="Deleting";});var file=(string)typeof(RetentionStore).GetField("_file",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f.Store);var restored=new RetentionStore(file,new TestJson());assert(restored.Read().Runs.Any(r=>r.State=="Interrupted") && restored.Read().Holds.Length==1,"Restart recovery cleared an uncertain attempt.");
            var replay=new CleanupCoordinator(restored,f.Catalog,f.Manager,()=>f.Now);replay.EvaluateNow("admin",p.Id);assert(restored.Read().Reviews.Single().ItemIds.All(id=>id!="1"),"A held target was automatically proposed again.");
        });
        test("Protection and cleanup endpoints reject missing or API-key-only native actor contexts",()=>
        {
            typeof(RetentionStore).GetField("_shared",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);typeof(CleanupRuntime).GetField("_coordinator",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);typeof(DeletionService).GetField("_manager",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
            var f=new NativeDeletionFixture(root){Played=false};AuthorizationInfo info=new();var request=Proxy.Create<IRequest>((m,a)=>null);var auth=Proxy.Create<IAuthorizationContext>((m,a)=>info);var paths=Proxy.Create<IApplicationPaths>((m,a)=>m.Name=="get_DataPath"?Path.Combine(root,"retention-service"):null);var service=new RetentionService(auth,f.Users,f.Data,f.Library,f.Sessions,f.Host,new TestJson(),paths);
            Reject(()=>service.Get(new GetRetention()));service.Request=request;Reject(()=>service.Post(new SaveProtection{ItemIds="1",Protected=true}));info=new AuthorizationInfo{User=f.Admin};var status=(CleanupStatus)service.Post(new SaveProtection{ItemIds="1",Protected=true});assert(status.Settings.Protected.Length==1 && ((ProtectionFlag[])service.Get(new GetProtection{ItemIds="1"}))[0].NeverDelete,"The native protected-state endpoint did not enforce the saved setting.");
            var store=(RetentionStore)typeof(RetentionStore).GetField("_shared",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);store.Change(s=>s.Holds=new[]{new CleanupHold{RunId="run",ItemId="1",Name="Fixture"}});Reject(()=>service.Post(new ClearCleanupHold{RunId="run",ItemId="1"}));assert(store.Read().Holds.Length==1,"A hold was silently cleared.");service.Post(new ClearCleanupHold{RunId="run",ItemId="1",Confirmed=true});assert(store.Read().Holds.Length==0 && f.NativeDeletes==0,"Hold release deleted media or ignored explicit confirmation.");
            var backend=new EmbyDeletionBackend(f.Users,f.Data,f.Library,f.Sessions,f.Host,store.Reason,store.LockProtection);assert(!f.Played && File.Exists(f.Movie.Path) && backend.Inspect("admin","alice","1",true,true).Item.State=="Blocked","Protecting an unwatched movie changed its watch flag or allowed opted-in deletion.");
        });
    }
}
public sealed class CleanupFixture
{
    public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-25T12:00:00Z");
    public RetentionStore Store;
    public CleanupCatalogFixture Catalog;
    public DeletionFixture Backend;
    public DeletionManager Manager;
    public CleanupCoordinator Coordinator;
    public CleanupFixture(string root) { Backend=new DeletionFixture(root);Store=new RetentionStore(Path.Combine(root,"cleanup-"+Guid.NewGuid().ToString("N"),"retention.json"),new TestJson());Catalog=new CleanupCatalogFixture(Backend);Manager=new DeletionManager(Backend,()=>Now);Coordinator=new CleanupCoordinator(Store,Catalog,Manager,()=>Now); }
    public CleanupPolicy Policy()=>new(){UserId="alice"};
}
public sealed class CleanupCatalogFixture:ICleanupCatalog
{
    private readonly DeletionFixture _backend;
    public bool Blocked,OwnerRevoked;
    public CleanupCatalogFixture(DeletionFixture backend){_backend=backend;}
    public BaseItem Item(string id)=>new Movie{InternalId=long.Parse(id),Name="Fixture "+id,Path=_backend.FileFor(id)};
    public string OwnerError(CleanupPolicy p)=>OwnerRevoked?"Owner revoked":null;
    public string[] Candidates(CleanupPolicy p,DateTimeOffset now,string[] events){if(OwnerRevoked)throw new InvalidOperationException("Owner revoked");return (events??new[]{"1","2","3"}).OrderBy(i=>i).ToArray();}
    public string Ineligible(CleanupPolicy p,string id,DateTimeOffset now)=>Blocked||OwnerRevoked?"Watch state or ownership changed":null;
}
