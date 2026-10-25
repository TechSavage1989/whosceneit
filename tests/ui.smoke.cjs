const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');

let browser;
let suiteCompleted = false;
process.on('exit', () => {
  if (!suiteCompleted && !process.exitCode) {
    console.error('FAIL browser suite exited before all checks completed');
    process.exitCode = 1;
  }
});
(async () => {
  browser = await chromium.launch({headless:true, executablePath:process.env.WSI_CHROMIUM || undefined, args:['--no-sandbox']});
  const page = await browser.newPage({viewport:{width:1280,height:1000},acceptDownloads:true});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.setContent('<body style="background:#171c20;color:#e8ecef;font-family:Arial,sans-serif">'+fs.readFileSync(path.join(__dirname,'../src/WhoSceneIt/Web/whosceneit.html'),'utf8')+'</body>');
  await page.evaluate(() => {
    window.calls=[];window.update={InstalledVersion:'1.2.2',LatestVersion:'1.3.0',UpdateAvailable:true,Downloaded:false,Busy:false,RestartRequired:false,CanRestart:true,AwaitingDashboardVerification:true,Message:'An update is available.',Notes:['Example change.'],HasMoreNotes:false,ReleaseUrl:'https://github.com/TechSavage1989/whosceneit/releases/tag/v1.3.0'};
    const connection=()=>({Enabled:false,BaseUrl:'',HasApiKey:false,EmbyPathRoot:'',ArrPathRoot:'',AddImportExclusion:true});
    window.arrSettings={Sonarr:{...connection(),HasApiKey:true},Radarr:connection()};
    window.deletionPlans={};window.deletionCounter=0;
    const row=(id,name,type='Movie')=>({ItemId:id,UserId:'alice',UserName:'Alice',Name:name,Type:type,Year:2024,Status:type==='Series'?'Watched episodes':'Watched',WatchedCount:type==='Series'?4:1,InProgressCount:0,SavedPlayCount:1,LastPlayedUtc:'2025-01-02T10:00:00.0000000+00:00'});
    window.sampleRows=[row('10','Example Show','Series'),row('11','<img src=x onerror="window.injected=true">'),row('12','=HYPERLINK("test")'),...Array.from({length:202},(_,i)=>row(String(i+100),'Movie '+String(i).padStart(3,'0')))];
    const episode=(id,name,fields)=>({...row(id,name,'Episode'),SeriesName:'Example Show',Episode:'S01E'+id.slice(-2),RunTimeTicks:18000000000,PlaybackPositionTicks:0,RemainingTicks:0,PlaybackPercent:100,DeletableCount:1,...fields});
    window.episodeRows=[episode('601','Completed episode',{}),episode('602','Partially watched episode',{Status:'Partially watched',WatchedCount:0,InProgressCount:1,PlaybackPositionTicks:9000000000,RemainingTicks:9000000000,PlaybackPercent:50,DeletableCount:0}),episode('603','Not-started episode',{Status:'Not started',WatchedCount:0,PlaybackPercent:0,RemainingTicks:18000000000,DeletableCount:0}),episode('604','Rewatching episode',{Status:'Watched with resume position',WatchedWithResumeCount:1,PlaybackPositionTicks:4500000000,PlaybackPercent:25,RemainingTicks:13500000000,DeletableCount:0}),episode('605','Unknown-duration episode',{Status:'Partially watched',WatchedCount:0,InProgressCount:1,PlaybackPositionTicks:1500000000,RunTimeTicks:null,RemainingTicks:null,PlaybackPercent:null,DeletableCount:0})];
    window.ApiClient={
      getUrl:(route,params)=>'https://local.invalid/'+route+'?'+new URLSearchParams(params),
      ajax: async options => {
        const u=new URL(options.url),p=Object.fromEntries(u.searchParams),body=options.data?JSON.parse(options.data):{};window.calls.push({path:u.pathname,params:p,body,method:options.type});
        const list=(rows)=>({Items:rows.slice(Number(p.StartIndex||0),Number(p.StartIndex||0)+Number(p.Limit||100)),TotalRecordCount:rows.length});
        if(u.pathname.endsWith('/Users'))return [{Id:'alice',Name:'Alice'},{Id:'bob',Name:'Bob'}];
        if(u.pathname.endsWith('/Integrations')){
          if(options.type==='POST')for(const name of ['Sonarr','Radarr']){const c=body[name],old=window.arrSettings[name];window.arrSettings[name]={Enabled:c.Enabled,BaseUrl:c.BaseUrl,EmbyPathRoot:c.EmbyPathRoot,ArrPathRoot:c.ArrPathRoot,AddImportExclusion:c.AddImportExclusion,HasApiKey:c.ClearApiKey?false:!!c.ApiKey||old.HasApiKey};}
          return structuredClone(window.arrSettings);
        }
        if(u.pathname.endsWith('/Integrations/Test'))return {Success:true,Message:'Read-only fixture connection test passed. Settings were not saved.'};
        if(u.pathname.endsWith('/UserReport')){
          if(window.reportFailure){if(window.reportFailureAsFetch)throw new Response(JSON.stringify(window.reportFailure.responseJSON),{status:window.reportFailure.status,headers:{'Content-Type':'application/json'}});throw structuredClone(window.reportFailure);}
          await new Promise(resolve=>setTimeout(resolve,p.UserId==='alice'?80:5));
          let rows=p.UserId==='bob'?[row('20','Bob Movie')]:window.sampleRows;
          if(p.View==='Episodes')rows=window.episodeRows.map(r=>({...r,UserId:p.UserId,UserName:p.UserId==='bob'?'Bob':'Alice'}));
          if(p.View==='Movies')rows=rows.filter(r=>r.Type==='Movie');
          if(p.View==='Shows')rows=rows.filter(r=>r.Type==='Series');
          if(p.WatchState==='Partial')rows=rows.filter(r=>!r.WatchedCount&&r.InProgressCount);
          if(p.WatchState==='Watched')rows=rows.filter(r=>r.WatchedCount);
          if(p.WatchState==='Resuming')rows=rows.filter(r=>r.WatchedWithResumeCount);
          if(p.WatchState==='NotStarted')rows=rows.filter(r=>!r.WatchedCount&&!r.InProgressCount);
          if(p.WatchState==='Started')rows=rows.filter(r=>r.WatchedCount||r.InProgressCount);
          if(p.SearchTerm)rows=rows.filter(r=>r.Name.toLowerCase().includes(p.SearchTerm.toLowerCase()));
          return list(rows);
        }
        if(u.pathname.endsWith('/Titles'))return list([{Id:'10',Name:'Example Show',Type:'Series',Year:2024},{Id:'100',Name:'Example Movie',Type:'Movie',Year:2024}]);
        if(u.pathname.endsWith('/Viewers'))return {...list([{...row('10','Example Show','Series'),UserName:'Alice',AvailableEpisodeCount:8},{...row('10','Example Show','Series'),UserId:'bob',UserName:'Bob',WatchedCount:8,Status:'All available episodes watched',AvailableEpisodeCount:8}]),Title:{Id:'10',Name:'Example Show',Type:'Series'}};
        if(u.pathname.endsWith('/Delete/Preview')){
          if(window.previewFailure)throw new Response(JSON.stringify({ResponseStatus:{Message:'Fixture preview failed: signed-in context unavailable <img src=x onerror="window.previewInjected=true">'}}),{status:500,headers:{'Content-Type':'application/json'}});
          if(window.previewEnvelope)return {State:'PreviewFailed',Message:'Fixture preview validation failed. No deletion was started.',Started:false};
          const ids=body.ItemIds.split(',').flatMap(id=>id==='10'?['400','401']:[id]);
          const items=ids.map(id=>({ItemId:id,Name:id==='400'?'Watched episode one':id==='401'?'Watched episode two':'Movie '+id,Type:'Movie',Paths:['/media/mock/'+id+'.mkv'],WatchedBy:['Alice'],State:'Ready'}));
          if(window.arrSettings.Radarr.Enabled)items.forEach(item=>Object.assign(item,{EmbyState:'Not deleted',ExternalService:'Radarr',ExternalState:'Ready',ExternalPath:'/movies/'+item.ItemId+'.mkv',ExternalMessage:'Unmonitor the matched movie and remove its Radarr entry after Emby deletion.'}));
          const plan={Token:'preview-'+(++window.deletionCounter),State:'Ready',Items:items,Ready:items.length,Total:items.length,Deleted:0,Skipped:0,Failed:0,Started:false,Processing:0,Processed:0,Message:'Review the exact media and paths.'};
          if(window.previewAllBlocked){plan.Items.forEach(i=>{i.State='Blocked';i.Message='Fixture deletion permission is unavailable';});plan.Ready=0;plan.Skipped=plan.Total;}
          window.deletionPlans[plan.Token]=plan;return structuredClone(plan);
        }
        if(u.pathname.endsWith('/Delete/Execute')){
          const plan=window.deletionPlans[body.Token];if(body.Confirmation!=='DELETE')throw new Error('Confirmation missing');
          plan.State='Deleting';plan.Started=true;plan.Processing=1;plan.CurrentItemName=plan.Items[0].Name;plan.Ready=plan.Total-1;plan.Items[0].State='Deleting';plan.Message='Deleting confirmed media...';
          if(window.executeReceiptLost){window.executeReceiptLost=false;throw new Response(JSON.stringify({ResponseStatus:{Message:'Fixture execution response lost'}}),{status:503});}
          return structuredClone(plan);
        }
        if(u.pathname.endsWith('/Delete/Status')){
          if(window.statusFailure){window.statusFailure=false;throw new Response(JSON.stringify({ResponseStatus:{Message:'Fixture status temporarily unavailable'}}),{status:503});}
          const plan=window.deletionPlans[p.Token];if(plan.State==='Deleting'){
            plan.Polls=(plan.Polls||0)+1;
            if(plan.Polls>1){plan.State='Completed';plan.Deleted=plan.Items.length;plan.Processed=plan.Total;plan.Ready=0;plan.Processing=0;plan.CurrentItemName=null;plan.Message='Deletion finished.';plan.SyncFailed=window.syncFailure?plan.Total:0;plan.Synchronized=window.syncFailure?0:plan.Total;plan.Items.forEach(item=>{item.State='Deleted';item.EmbyState='Deleted';if(item.ExternalService){item.ExternalState=window.syncFailure?'Failed':'Completed';item.ExternalMessage=window.syncFailure?'Fixture Radarr cleanup failed <img src=x onerror="window.syncInjected=true">':'Movie entry removed.';}});
            window.sampleRows=window.sampleRows.filter(row=>!plan.Items.some(item=>item.ItemId===row.ItemId));}
          }return structuredClone(plan);
        }
        if(u.pathname.endsWith('/Delete/Cancel')){const plan=window.deletionPlans[body.Token];plan.State='Cancelled';plan.Message='Deletion cancelled.';return structuredClone(plan);}
        if(u.pathname.endsWith('/Updates/Download')){window.update.Downloaded=true;window.update.Message='Download verified. Ready to install.';return {...window.update};}
        if(u.pathname.endsWith('/Updates/Install')){window.update.RestartRequired=true;window.update.Downloaded=false;window.update.Message='Update installed. Restarting Emby Server...';return {...window.update};}
        if(u.pathname.endsWith('/Updates/Check'))return {...window.update};
        if(u.pathname.endsWith('/Updates/Healthy')){window.update.AwaitingDashboardVerification=false;return {...window.update};}
        if(u.pathname.endsWith('/Updates'))return {...window.update};
        throw new Error('Unexpected route '+u.pathname);
      }
    };
    window.define=(deps,factory)=>{window.controller=factory();};
  });
  await page.addScriptTag({path:path.join(__dirname,'../src/WhoSceneIt/Web/whosceneit.js')});
  await page.evaluate(()=>{const view=document.querySelector('[data-role=page]');window.controller(view);view.dispatchEvent(new Event('viewshow'));});
  const get=name=>page.locator('[data-id="'+name+'"]');
  await get('reportStatus').filter({hasText:'205 matching titles'}).waitFor();
  assert.equal(await get('reportBody').locator('tr').count(),100);
  assert.equal(await page.evaluate(()=>window.injected),undefined);
  console.log('PASS initial user report, paging, and safe title rendering');
  await get('integrationSettings').locator('summary').click();
  await get('integrationSummary').filter({hasText:'Emby-only deletion'}).waitFor();
  assert.equal(await get('sonarrKey').inputValue(),'');assert.match(await get('sonarrKeyStatus').innerText(),/A key is stored/);
  await get('radarrEnabled').check();await get('radarrUrl').fill('https://radarr.fixture.invalid/base');await get('radarrKey').fill('fixture-ui-api-key');
  await get('radarrEmbyRoot').fill('/media/movies');await get('radarrArrRoot').fill('/movies');
  await get('testRadarr').click();await get('integrationStatus').filter({hasText:'Read-only fixture connection test passed'}).waitFor();
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Integrations')&&c.method==='POST').length),0);
  const testConnection=await page.evaluate(()=>window.calls.find(c=>c.path.endsWith('/Integrations/Test')));
  assert.equal(testConnection.body.Service,'Radarr');assert.equal(testConnection.body.Connection.ApiKey,'fixture-ui-api-key');
  await get('saveIntegrations').click();await get('integrationStatus').filter({hasText:'Settings saved.'}).waitFor();
  assert.equal(await get('radarrKey').inputValue(),'');assert.match(await get('radarrKeyStatus').innerText(),/A key is stored/);
  const savedConnections=await page.evaluate(()=>window.calls.find(c=>c.path.endsWith('/Integrations')&&c.method==='POST'));
  assert.equal(savedConnections.body.Sonarr.ApiKey,'');assert.equal(savedConnections.body.Radarr.ArrPathRoot,'/movies');
  await get('radarrUrl').fill('https://unsaved.fixture.invalid');await get('reloadIntegrations').click();
  await page.waitForFunction(()=>document.querySelector('[data-id=radarrUrl]').value==='https://radarr.fixture.invalid/base');
  if(process.env.WSI_ARR_SCREENSHOT)await get('integrationSettings').screenshot({path:process.env.WSI_ARR_SCREENSHOT});
  await get('integrationSettings').locator('summary').click();
  console.log('PASS synchronization settings keep stored keys masked, test unsaved connections read-only, save paths, and reload saved values');
  await get('next').click();await get('pageInfo').filter({hasText:'101-200 of 205'}).waitFor();
  await get('next').click();await get('pageInfo').filter({hasText:'201-205 of 205'}).waitFor();
  const downloadPromise=page.waitForEvent('download');await get('export').click();const download=await downloadPromise;
  const csv=fs.readFileSync(await download.path(),'utf8');
  assert.equal(csv.trim().split('\r\n').length,206);assert.ok(csv.includes("'=HYPERLINK"));
  console.log('PASS CSV includes every page and neutralizes formula-like titles');
  await get('reportBody').locator('[data-item-selection="300"]').check();
  await get('prev').click();await get('pageInfo').filter({hasText:'101-200 of 205'}).waitFor();
  await get('reportBody').locator('[data-item-selection="200"]').check();
  assert.equal(await get('selectionCount').innerText(),'2 selected');
  await get('deleteSelected').click();await get('deleteSummary').filter({hasText:'2 ready'}).waitFor();
  if(process.env.WSI_DELETE_SCREENSHOT)await page.screenshot({path:process.env.WSI_DELETE_SCREENSHOT,fullPage:false});
  assert.equal(await get('confirmDelete').isEnabled(),false);
  await get('deleteWord').fill('DELETE');assert.equal(await get('confirmDelete').isEnabled(),false);
  await get('deleteAcknowledge').check();assert.equal(await get('confirmDelete').isEnabled(),true);
  await get('cancelDelete').click();await get('deleteDialog').waitFor({state:'hidden'});
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Delete/Execute')).length),0);
  console.log('PASS bulk selection carries across pages and preview cancellation never deletes media');
  await get('reportBody').getByRole('button',{name:'Review deletion of Movie 100',exact:true}).click();
  await get('deleteSummary').filter({hasText:'1 ready'}).waitFor();
  assert.match(await get('deleteItems').innerText(),/\/media\/mock\/200.mkv/);
  await get('cancelDelete').click();await get('deleteDialog').waitFor({state:'hidden'});
  console.log('PASS individual delete buttons preview exactly the requested movie');
  await page.evaluate(()=>window.previewFailure=true);await get('deleteSelected').click();await get('deletePhase').filter({hasText:'Preview failed. No deletion was started'}).waitFor();
  assert.match(await get('deleteSummary').innerText(),/Fixture preview failed/);assert.equal(await get('deleteSummary').locator('img').count(),0);assert.equal(await page.evaluate(()=>window.previewInjected),undefined);
  assert.equal(await get('confirmDelete').isVisible(),false);assert.equal(await get('deleteProgressArea').isVisible(),false);assert.equal(await get('retryPreview').isVisible(),true);
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Delete/Execute')).length),0);
  if(process.env.WSI_DELETE_FAILURE_SCREENSHOT)await page.screenshot({path:process.env.WSI_DELETE_FAILURE_SCREENSHOT,fullPage:false});
  await page.evaluate(()=>{window.previewFailure=false;window.previewEnvelope=true;});await get('retryPreview').click();await get('deleteSummary').filter({hasText:'Fixture preview validation failed'}).waitFor();
  await page.evaluate(()=>window.previewEnvelope=false);await get('retryPreview').click();await get('deletePhase').filter({hasText:'Step 2 of 3'}).waitFor();
  assert.equal(await get('deleteAcknowledge').isChecked(),false);assert.equal(await get('deleteWord').inputValue(),'');assert.equal(await get('confirmDelete').isEnabled(),false);
  await get('cancelDelete').click();await get('deleteDialog').waitFor({state:'hidden'});
  console.log('PASS native Fetch preview errors and structured failures show no-deletion state and retry only the read-only preview');
  await page.evaluate(()=>window.previewAllBlocked=true);await get('deleteSelected').click();await get('deletePhase').filter({hasText:'Nothing can be deleted'}).waitFor();
  assert.equal(await get('deleteConfirmFields').isVisible(),false);assert.equal(await get('deleteProgressArea').isVisible(),false);assert.match(await get('deleteItems').innerText(),/Fixture deletion permission/);
  await get('cancelDelete').click();await get('deleteDialog').waitFor({state:'hidden'});await page.evaluate(()=>window.previewAllBlocked=false);
  console.log('PASS all-blocked previews explain each blocked item and never offer a destructive confirmation');
  await page.evaluate(()=>{window.executeReceiptLost=true;window.statusFailure=true;});
  await get('deleteSelected').click();await get('deleteSummary').filter({hasText:'2 ready'}).waitFor();
  await get('deleteAcknowledge').check();await get('deleteWord').fill('DELETE');await get('confirmDelete').click();
  await get('deletePhase').filter({hasText:'Deletion status is unavailable'}).waitFor();assert.equal(await get('retryPreview').isVisible(),false);assert.equal(await get('confirmDelete').isVisible(),false);
  await get('deletePhase').filter({hasText:'Step 3 of 3: Deleting'}).waitFor();assert.equal(await get('deleteProgressArea').isVisible(),true);assert.match(await get('deleteProgressText').innerText(),/Current item: Movie/);
  if(process.env.WSI_DELETE_PROGRESS_SCREENSHOT)await page.screenshot({path:process.env.WSI_DELETE_PROGRESS_SCREENSHOT,fullPage:false});
  await get('closeDelete').click();await get('deleteDialog').waitFor({state:'hidden'});assert.equal(await get('viewDeletion').isVisible(),true);await get('viewDeletion').click();
  await get('deleteSummary').filter({hasText:'2 deleted; 0 ready'}).waitFor();
  assert.match(await get('deletePhase').innerText(),/Deletion completed/);assert.equal(await get('deleteProgress').getAttribute('value'),'2');
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Delete/Execute')).length),1);
  const deletionCall=await page.evaluate(()=>window.calls.find(c=>c.path.endsWith('/Delete/Execute')));
  assert.equal(deletionCall.method,'POST');assert.equal(deletionCall.body.Confirmation,'DELETE');
  await get('closeDelete').click();await get('reportStatus').filter({hasText:'203 matching titles'}).waitFor();
  assert.equal(await get('selectionCount').innerText(),'0 selected');
  console.log('PASS confirmed deletion shows live progress, recovers lost responses without replay, and reports final results');
  await page.evaluate(()=>window.syncFailure=true);
  await get('reportBody').locator('[data-item-selection="202"]').check();await get('deleteSelected').click();
  await get('deletePhase').filter({hasText:'Step 2 of 3'}).waitFor();assert.match(await get('deleteItems').innerText(),/Radarr: Ready/);assert.match(await get('deleteItems').innerText(),/Matched service path: \/movies\/202/);
  await get('deleteAcknowledge').check();await get('deleteWord').fill('DELETE');await get('confirmDelete').click();
  await get('deletePhase').filter({hasText:'Deletion finished with items to review'}).waitFor();
  assert.match(await get('deleteSummary').innerText(),/1 deleted; 0 ready.*1 synchronization issues/);
  assert.match(await get('deleteItems').innerText(),/Emby: Deleted/);assert.match(await get('deleteItems').innerText(),/Radarr: Failed/);
  assert.equal(await get('deleteItems').locator('img').count(),0);assert.equal(await page.evaluate(()=>window.syncInjected),undefined);
  if(process.env.WSI_ARR_FAILURE_SCREENSHOT)await page.screenshot({path:process.env.WSI_ARR_FAILURE_SCREENSHOT,fullPage:false});
  await get('closeDelete').click();await get('reportStatus').filter({hasText:'202 matching titles'}).waitFor();await page.evaluate(()=>window.syncFailure=false);
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Delete/Execute')).length),2);
  console.log('PASS preview lists exact service actions and partial synchronization failures retain truthful Emby results without replay');
  await get('reportBody').locator('[data-item-selection="201"]').check();
  await get('filter').fill('Movie 101');await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();
  assert.equal(await get('selectionCount').innerText(),'0 selected');
  await get('filter').fill('');await get('reportStatus').filter({hasText:'202 matching titles'}).waitFor();
  console.log('PASS report scope and filter changes clear stale bulk selections');
  await get('viewSelect').selectOption('Shows');await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();
  assert.match(await get('reportBody').innerText(),/4 watched episodes/);
  await get('viewSelect').selectOption('Titles');
  await get('userSelect').selectOption('bob');await get('reportBody').getByText('Bob Movie',{exact:true}).waitFor();
  await get('userSelect').selectOption('alice');await get('userSelect').selectOption('bob');
  await page.waitForTimeout(200);assert.match(await get('reportBody').innerText(),/Bob Movie/);
  console.log('PASS show grouping and stale-request suppression during user changes');
  await get('titleTab').click();await get('titleSearch').fill('Example');await get('titleSearchForm').getByRole('button',{name:'Search titles'}).click();
  await get('titleResults').getByRole('button').first().click();await get('reportStatus').filter({hasText:'2 matching users'}).waitFor();
  assert.match(await get('reportBody').innerText(),/Alice/);assert.match(await get('reportBody').innerText(),/Bob/);assert.match(await get('reportBody').innerText(),/8 watched episodes of 8 available/);
  console.log('PASS title selection and users who watched a show');
  assert.equal(await get('deleteSelected').isVisible(),false);
  await get('deleteTitle').click();await get('deleteSummary').filter({hasText:'2 ready'}).waitFor();
  assert.match(await get('deleteItems').innerText(),/Watched episode one/);
  const titlePreview=await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Delete/Preview')).at(-1));
  assert.equal(titlePreview.body.UserId,null);assert.equal(titlePreview.body.ItemIds,'10');
  await get('cancelDelete').click();await get('deleteDialog').waitFor({state:'hidden'});
  console.log('PASS title view previews watched episode media without deleting a TV show folder');
  await page.evaluate(()=>window.sampleRows.push({ItemId:'999',UserId:'alice',UserName:'Alice',Name:'Partial Movie',Type:'Movie',Status:'In progress',WatchedCount:0,InProgressCount:1,SavedPlayCount:0}));
  await get('userTab').click();await get('userSelect').selectOption('alice');await get('filter').fill('Partial Movie');
  await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();
  assert.equal(await get('reportBody').locator('[data-item-selection="999"]').count(),0);
  assert.equal(await get('reportBody').getByRole('button',{name:'Review deletion of Partial Movie',exact:true}).isEnabled(),false);
  await get('filter').fill('');await get('titleTab').click();await get('reportStatus').filter({hasText:'2 matching users'}).waitFor();
  console.log('PASS in-progress-only titles have no selection checkbox and cannot be deleted');
  await get('userTab').click();await get('userSelect').selectOption('alice');await get('viewSelect').selectOption('Shows');
  await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();
  await get('reportBody').getByRole('button',{name:'View episodes for Example Show',exact:true}).click();
  await get('reportStatus').filter({hasText:'4 matching titles'}).waitFor();
  assert.equal(await get('episodeScope').isVisible(),true);
  assert.match(await get('reportBody').innerText(),/50% saved position/);
  assert.match(await get('reportBody').innerText(),/15:00 of 30:00; 15:00 remaining/);
  assert.match(await get('reportBody').innerText(),/Resume at 2:30/);
  assert.match(await get('reportBody').innerText(),/percentage and remaining time unavailable/);
  const scoped=await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/UserReport')).at(-1));
  assert.equal(scoped.params.SeriesId,'10');assert.equal(scoped.params.SortBy,'Episode');
  console.log('PASS show drilldown scopes the episode list and displays saved progress without inventing durations');
  await get('watchState').selectOption('Partial');await get('reportStatus').filter({hasText:'2 matching titles'}).waitFor();
  assert.equal(await get('reportBody').locator('[data-item-selection]').count(),0);
  const progressDownload=page.waitForEvent('download');await get('export').click();
  const progressCsv=fs.readFileSync(await (await progressDownload).path(),'utf8');
  assert.equal(progressCsv.trim().split('\r\n').length,3);assert.ok(progressCsv.includes('Remaining time'));assert.ok(progressCsv.includes('15:00'));assert.ok(progressCsv.includes('Playback percent'));
  assert.equal((await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/UserReport')).at(-1))).params.WatchState,'Partial');
  console.log('PASS partial filter and CSV preserve episode scope, position, duration, and remaining time');
  await get('watchState').selectOption('Resuming');await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();
  assert.match(await get('reportBody').innerText(),/Watched with resume position/);
  assert.equal(await get('reportBody').locator('[data-item-selection]').count(),0);
  assert.equal(await get('reportBody').getByRole('button',{name:'Review deletion of Rewatching episode',exact:true}).isEnabled(),false);
  console.log('PASS rewatch filter retains prior completion while disabling media deletion');
  await get('watchState').selectOption('NotStarted');await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();assert.match(await get('reportBody').innerText(),/Not-started episode/);
  await get('watchState').selectOption('All');await get('reportStatus').filter({hasText:'5 matching titles'}).waitFor();
  if(process.env.WSI_PROGRESS_SCREENSHOT)await page.screenshot({path:process.env.WSI_PROGRESS_SCREENSHOT,fullPage:true});
  await get('watchState').selectOption('Watched');await get('reportStatus').filter({hasText:'2 matching titles'}).waitFor();
  await get('watchState').selectOption('Started');await get('clearSeries').click();await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();assert.equal(await get('episodeScope').isVisible(),false);
  console.log('PASS all, completed, and not-started filters work and clearing episode scope returns to shows');
  await get('titleTab').click();await get('reportStatus').filter({hasText:'2 matching users'}).waitFor();
  await get('reportBody').getByRole('button',{name:'View episodes for Bob',exact:true}).click();await get('reportStatus').filter({hasText:'4 matching titles'}).waitFor();
  assert.equal(await get('userSelect').inputValue(),'bob');
  assert.equal((await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/UserReport')).at(-1))).params.SeriesId,'10');
  await get('clearSeries').click();await get('userSelect').selectOption('alice');await get('titleTab').click();await get('reportStatus').filter({hasText:'2 matching users'}).waitFor();
  console.log('PASS viewer drilldown keeps the chosen user and show together');
  await page.evaluate(()=>{window.reportFailureAsFetch=true;window.reportFailure={status:500,responseJSON:{ResponseStatus:{Message:'Fixture query failed: <img src=x onerror="window.errorInjected=true">',StackTrace:'DO_NOT_SHOW_STACK_TRACE'}}};window.update.AwaitingDashboardVerification=true;});
  await get('userTab').click();await get('reportStatus').filter({hasText:'Fixture query failed:'}).waitFor();
  assert.match(await get('reportStatus').innerText(),/HTTP 500/);assert.equal(await get('reportStatus').locator('img').count(),0);
  assert.equal(await page.evaluate(()=>window.errorInjected),undefined);assert.ok(!(await get('reportStatus').innerText()).includes('DO_NOT_SHOW_STACK_TRACE'));
  assert.equal(await get('export').isEnabled(),false);assert.equal(await get('deleteSelected').isEnabled(),false);
  const healthyBeforeFailure=await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Updates/Healthy')).length);
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));await page.waitForTimeout(100);
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Updates/Healthy')).length),healthyBeforeFailure);
  await page.evaluate(()=>{window.reportFailureAsFetch=false;window.reportFailure={status:502,responseText:'<html>Fixture gateway error</html>'};});
  await get('refresh').click();await get('reportStatus').filter({hasText:'HTTP 502'}).waitFor();assert.ok(!(await get('reportStatus').innerText()).includes('<html>'));
  await page.evaluate(()=>window.reportFailure={status:403,responseJSON:{ResponseStatus:{Message:'Do not show this authorization detail'}}});
  await get('refresh').click();await get('reportStatus').filter({hasText:'Administrator access is required'}).waitFor();
  await page.evaluate(()=>delete window.reportFailure);await get('refresh').click();await get('reportStatus').filter({hasText:'1 matching titles'}).waitFor();
  await get('titleTab').click();await get('reportStatus').filter({hasText:'2 matching users'}).waitFor();
  console.log('PASS report errors show safe native messages and HTTP status, preserve auth guidance, and never acknowledge failed report health');
  assert.equal(await get('releaseLink').isVisible(),false);
  await get('downloadUpdate').click();await get('installUpdate').waitFor({state:'visible'});
  assert.equal(await get('restartWarning').isVisible(),true);
  await get('installUpdate').click();await get('updateMessage').filter({hasText:'Restarting'}).waitFor();
  const installs=await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Updates/Install')));
  assert.equal(installs.length,1);assert.equal(installs[0].method,'POST');
  console.log('PASS explicit download and install controls, restart warning, and hidden empty Read more');
  await page.evaluate(()=>{window.update.RestartRequired=false;window.update.Downloaded=false;});
  const focusBefore=await page.evaluate(()=>window.calls.filter(c=>c.params.Reason==='focus').length);
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));
  await page.waitForTimeout(100);
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.params.Reason==='focus').length),focusBefore+1);
  await page.evaluate(()=>document.querySelector('[data-role=page]').dispatchEvent(new Event('viewhide')));
  const hiddenBefore=await page.evaluate(()=>window.calls.length);
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));await page.waitForTimeout(100);
  assert.equal(await page.evaluate(()=>window.calls.length),hiddenBefore);
  await page.evaluate(()=>document.querySelector('[data-role=page]').dispatchEvent(new Event('viewshow')));
  await page.waitForTimeout(100);
  const returnBefore=await page.evaluate(()=>window.calls.filter(c=>c.params.Reason==='focus').length);
  await page.evaluate(()=>window.dispatchEvent(new Event('focus')));await page.waitForTimeout(100);
  assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.params.Reason==='focus').length),returnBefore+1);
  console.log('PASS real browser focus events and listener cleanup without duplicate subscriptions');
  await page.setViewportSize({width:390,height:844});
  await get('integrationSettings').locator('summary').click();
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));
  await get('integrationSettings').locator('summary').click();
  await page.setViewportSize({width:1280,height:1000});
  if(process.env.WSI_SCREENSHOT)await page.screenshot({path:process.env.WSI_SCREENSHOT,fullPage:true});
  assert.deepEqual(errors,[]);
  console.log('PASS mobile layout and no browser exceptions');
  const stale=await browser.newPage();await stale.setContent('<div data-role="page"><p>Old cached dashboard</p></div>');
  assert.equal(await page.locator('[data-role=page]').getAttribute('data-controller'),'__plugin/whosceneit-1.3.0.js');
  await stale.evaluate(()=>{window.define=(deps,factory)=>{window.controller=factory();};});await stale.addScriptTag({path:path.join(__dirname,'../src/WhoSceneIt/Web/whosceneit.js')});
  await stale.evaluate(()=>window.controller(document.querySelector('[data-role=page]')));assert.match(await stale.locator('body').innerText(),/Reload this page to load the current deletion and synchronization controls/);await stale.close();
  console.log('PASS outdated cached HTML gets a reload notice and the dashboard uses a versioned controller');
  if(process.argv.includes('--real-timers')){
    const before=await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Updates')).length);
    await page.waitForTimeout(31000);
    assert.ok(await page.evaluate(()=>window.calls.filter(c=>c.path.endsWith('/Updates')).length)>before);
    console.log('PASS real browser update-status polling timer');
    await page.evaluate(()=>document.querySelector('[data-role=page]').dispatchEvent(new Event('viewhide')));
    const stopped=await page.evaluate(()=>window.calls.length);await page.waitForTimeout(31000);
    assert.equal(await page.evaluate(()=>window.calls.length),stopped);
    console.log('PASS browser timers stop after leaving the plugin page');
  }
  await browser.close();
  suiteCompleted = true;
  console.log('RESULT ' + (process.argv.includes('--real-timers') ? 26 : 24) + ' browser checks passed');
})().catch(async error=>{console.error(error);if(browser)await browser.close();process.exitCode=1;});
