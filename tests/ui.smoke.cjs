const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');

let browser;
(async () => {
  browser = await chromium.launch({headless:true, executablePath:process.env.WSI_CHROMIUM || undefined, args:['--no-sandbox']});
  const page = await browser.newPage({viewport:{width:1280,height:1000},acceptDownloads:true});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.setContent('<body style="background:#171c20;color:#e8ecef;font-family:Arial,sans-serif">'+fs.readFileSync(path.join(__dirname,'../src/WhoSceneIt/Web/whosceneit.html'),'utf8')+'</body>');
  await page.evaluate(() => {
    window.calls=[];window.update={InstalledVersion:'1.0.0',LatestVersion:'1.0.1',UpdateAvailable:true,Downloaded:false,Busy:false,RestartRequired:false,CanRestart:true,AwaitingDashboardVerification:true,Message:'An update is available.',Notes:['Example change.'],HasMoreNotes:false,ReleaseUrl:'https://github.com/TechSavage1989/whosceneit/releases/tag/v1.0.1'};
    const row=(id,name,type='Movie')=>({ItemId:id,UserId:'alice',UserName:'Alice',Name:name,Type:type,Year:2024,Status:type==='Series'?'Watched episodes':'Watched',WatchedCount:type==='Series'?4:1,InProgressCount:0,SavedPlayCount:1,LastPlayedUtc:'2025-01-02T10:00:00.0000000+00:00'});
    window.sampleRows=[row('10','Example Show','Series'),row('11','<img src=x onerror="window.injected=true">'),row('12','=HYPERLINK("test")'),...Array.from({length:202},(_,i)=>row(String(i+100),'Movie '+String(i).padStart(3,'0')))];
    window.ApiClient={
      getUrl:(route,params)=>'https://local.invalid/'+route+'?'+new URLSearchParams(params),
      ajax: async options => {
        const u=new URL(options.url),p=Object.fromEntries(u.searchParams);window.calls.push({path:u.pathname,params:p,method:options.type});
        const list=(rows)=>({Items:rows.slice(Number(p.StartIndex||0),Number(p.StartIndex||0)+Number(p.Limit||100)),TotalRecordCount:rows.length});
        if(u.pathname.endsWith('/Users'))return [{Id:'alice',Name:'Alice'},{Id:'bob',Name:'Bob'}];
        if(u.pathname.endsWith('/UserReport')){
          await new Promise(resolve=>setTimeout(resolve,p.UserId==='alice'?80:5));
          let rows=p.UserId==='bob'?[row('20','Bob Movie')]:window.sampleRows;
          if(p.View==='Movies')rows=rows.filter(r=>r.Type==='Movie');
          if(p.View==='Shows')rows=rows.filter(r=>r.Type==='Series');
          if(p.SearchTerm)rows=rows.filter(r=>r.Name.toLowerCase().includes(p.SearchTerm.toLowerCase()));
          return list(rows);
        }
        if(u.pathname.endsWith('/Titles'))return list([{Id:'10',Name:'Example Show',Type:'Series',Year:2024},{Id:'100',Name:'Example Movie',Type:'Movie',Year:2024}]);
        if(u.pathname.endsWith('/Viewers'))return {...list([{...row('10','Example Show','Series'),UserName:'Alice',AvailableEpisodeCount:8},{...row('10','Example Show','Series'),UserId:'bob',UserName:'Bob',WatchedCount:8,Status:'All available episodes watched',AvailableEpisodeCount:8}]),Title:{Id:'10',Name:'Example Show',Type:'Series'}};
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
  await get('next').click();await get('pageInfo').filter({hasText:'101-200 of 205'}).waitFor();
  await get('next').click();await get('pageInfo').filter({hasText:'201-205 of 205'}).waitFor();
  const downloadPromise=page.waitForEvent('download');await get('export').click();const download=await downloadPromise;
  const csv=fs.readFileSync(await download.path(),'utf8');
  assert.equal(csv.trim().split('\r\n').length,206);assert.ok(csv.includes("'=HYPERLINK"));
  console.log('PASS CSV includes every page and neutralizes formula-like titles');
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
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));
  await page.setViewportSize({width:1280,height:1000});
  if(process.env.WSI_SCREENSHOT)await page.screenshot({path:process.env.WSI_SCREENSHOT,fullPage:true});
  assert.deepEqual(errors,[]);
  console.log('PASS mobile layout and no browser exceptions');
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
})().catch(async error=>{console.error(error);if(browser)await browser.close();process.exitCode=1;});
