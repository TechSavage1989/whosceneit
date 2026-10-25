define([], function () {
    'use strict';
    return function (view) {
        var find = function (name) { return view.querySelector('[data-id="' + name + '"]'); };
        var state = { mode: 'user', start: 0, total: 0, titleStart: 0, selected: null, token: 0, titleToken: 0, active: false, initialized: false, busy: false, exporting: false, reportHealthy: false };
        var pollTimer = null, filterTimer = null;
        var pageSize = 100;
        function api(path, data, method) {
            return ApiClient.ajax({ type: method || 'GET', url: ApiClient.getUrl('WhoSceneIt/' + path, data || {}), dataType: 'json' });
        }
        function text(node, value) { node.textContent = value == null ? '' : String(value); }
        function errorText(error) {
            if (error && (error.status === 401 || error.status === 403)) return 'Administrator access is required. Sign in again with an administrator account.';
            return 'The request failed. Check your connection and Emby Server logs, then try again.';
        }
        function status(message, error) { text(find('reportStatus'), message); find('reportStatus').classList.toggle('wsi-error', !!error); }
        function reportParams(start, limit) {
            var values = { StartIndex: start, Limit: limit, IncludeInProgress: find('includeProgress').checked, SearchTerm: find('filter').value.trim(), SortBy: find('sort').value };
            if (state.mode === 'user') { values.UserId = find('userSelect').value; values.View = find('viewSelect').value; }
            else values.ItemId = state.selected.Id;
            return values;
        }
        function changeMode(mode) {
            state.mode = mode; state.start = 0; state.token++;
            find('userPanel').hidden = mode !== 'user'; find('titlePanel').hidden = mode !== 'title';
            find('userTab').setAttribute('aria-selected', String(mode === 'user'));
            find('titleTab').setAttribute('aria-selected', String(mode === 'title'));
            clearReport(); loadReport();
        }
        function clearReport() {
            state.total = 0; find('reportBody').replaceChildren(); find('tableWrap').hidden = true;
            find('pager').hidden = true; find('export').disabled = true;
        }
        function cell(row, value, small) {
            var td = document.createElement('td'); text(td, value);
            if (small) { var detail = document.createElement('small'); text(detail, small); td.appendChild(detail); }
            row.appendChild(td); return td;
        }
        function dateLabel(value) {
            if (!value) return 'Not recorded';
            var date = new Date(value); return isNaN(date.getTime()) ? 'Not recorded' : date.toLocaleString();
        }
        function renderReport(result) {
            var rows = result.Items || [], byTitle = state.mode === 'title';
            state.total = result.TotalRecordCount || 0;
            var headings = byTitle ? ['User', 'Watch status', 'Watched / in progress', 'Saved play count', 'Last played'] : ['Title', 'Type', 'Watch status', 'Watched / in progress', 'Saved play count', 'Last played'];
            var tr = document.createElement('tr');
            headings.forEach(function (label) { var th = document.createElement('th'); th.scope = 'col'; text(th, label); tr.appendChild(th); });
            find('reportHead').replaceChildren(tr); find('reportBody').replaceChildren();
            rows.forEach(function (item) {
                var row = document.createElement('tr');
                if (byTitle) cell(row, item.UserName, item.UserDisabled ? 'Disabled account' : '');
                else {
                    var nameCell = cell(row, '', item.Type === 'Episode' ? [item.SeriesName, item.Episode].filter(Boolean).join(' - ') : (item.Year || ''));
                    var titleButton = document.createElement('button'); titleButton.type = 'button'; titleButton.className = 'wsi-link'; text(titleButton, item.Name);
                    titleButton.addEventListener('click', function () { state.selected = { Id: item.ItemId, Name: item.Name, Type: item.Type, Year: item.Year }; showSelected(); changeMode('title'); });
                    nameCell.prepend(titleButton); cell(row, item.Type === 'Series' ? 'TV show' : item.Type);
                }
                cell(row, item.Status);
                var count = item.WatchedCount + ' watched';
                if (item.Type === 'Series') count += ' episodes';
                if (item.AvailableEpisodeCount != null) count += ' of ' + item.AvailableEpisodeCount + ' available';
                if (item.InProgressCount) count += '; ' + item.InProgressCount + ' in progress';
                cell(row, count); cell(row, item.SavedPlayCount || 0); cell(row, dateLabel(item.LastPlayedUtc));
                find('reportBody').appendChild(row);
            });
            find('tableWrap').hidden = !rows.length; find('pager').hidden = state.total <= pageSize;
            find('prev').disabled = state.start <= 0; find('next').disabled = state.start + rows.length >= state.total;
            text(find('pageInfo'), (state.start + 1) + '-' + (state.start + rows.length) + ' of ' + state.total);
            find('export').disabled = !state.total || state.exporting;
            status(state.total ? state.total + ' matching ' + (byTitle ? 'users' : 'titles') + '. Dates use your browser time zone.' : 'No matching results.');
        }
        function loadReport() {
            if (!state.active) return Promise.resolve();
            if (state.mode === 'title' && !state.selected) { clearReport(); status('Search for a title and select a result.'); return Promise.resolve(); }
            if (state.mode === 'user' && !find('userSelect').value) { clearReport(); status('No Emby users were found.'); return Promise.resolve(); }
            var token = ++state.token, path = state.mode === 'user' ? 'UserReport' : 'Viewers';
            state.busy = true; find('export').disabled = true; status('Loading report...');
            return api(path, reportParams(state.start, pageSize)).then(function (result) {
                if (token !== state.token || !state.active) return;
                state.busy = false;
                state.reportHealthy = true;
                if (!result.Items.length && state.start > 0 && result.TotalRecordCount > 0) { state.start = Math.floor((result.TotalRecordCount - 1) / pageSize) * pageSize; return loadReport(); }
                renderReport(result);
            }).catch(function (error) { if (token === state.token && state.active) { state.busy = false; clearReport(); status(errorText(error), true); } });
        }
        function showSelected() {
            find('selectedTitle').hidden = !state.selected;
            if (state.selected) text(find('selectedTitle'), 'Selected: ' + state.selected.Name + (state.selected.Year ? ' (' + state.selected.Year + ')' : '') + ' - ' + (state.selected.Type === 'Series' ? 'TV show' : state.selected.Type));
        }
        function searchTitles(start) {
            var term = find('titleSearch').value.trim(), token = ++state.titleToken;
            if (!term) return;
            state.titleStart = start; text(find('titleResults'), 'Searching...'); find('titlePager').hidden = true;
            api('Titles', { SearchTerm: term, Type: find('titleType').value, StartIndex: start, Limit: 30 }).then(function (result) {
                if (token !== state.titleToken || !state.active) return;
                find('titleResults').replaceChildren();
                result.Items.forEach(function (item) {
                    var button = document.createElement('button'); button.type = 'button';
                    text(button, item.Name + (item.Year ? ' (' + item.Year + ')' : ''));
                    var detail = document.createElement('small'); text(detail, [item.Type === 'Series' ? 'TV show' : item.Type, item.SeriesName, item.Id].filter(Boolean).join(' - ')); button.appendChild(detail);
                    button.addEventListener('click', function () { state.selected = item; state.start = 0; showSelected(); loadReport(); });
                    find('titleResults').appendChild(button);
                });
                if (!result.Items.length) text(find('titleResults'), 'No titles found.');
                find('titlePager').hidden = result.TotalRecordCount <= 30; find('titlePrev').disabled = start === 0; find('titleNext').disabled = start + result.Items.length >= result.TotalRecordCount;
                text(find('titlePageInfo'), (start + 1) + '-' + (start + result.Items.length) + ' of ' + result.TotalRecordCount);
            }).catch(function (error) { if (token === state.titleToken) text(find('titleResults'), errorText(error)); });
        }
        function csvCell(value) {
            var str = value == null ? '' : String(value);
            if (/^[\s]*[=+@-]/.test(str) || /^[\t\r\n]/.test(str)) str = "'" + str;
            return '"' + str.replace(/"/g, '""') + '"';
        }
        function exportCsv() {
            if (state.exporting || !state.total || state.busy) return;
            var path = state.mode === 'user' ? 'UserReport' : 'Viewers', parameters = reportParams(0, 500), all = [];
            state.exporting = true; find('export').disabled = true;
            function next() {
                status('Preparing CSV: ' + all.length + ' rows...');
                return api(path, parameters).then(function (result) {
                    if (!result.Items.length && parameters.StartIndex < result.TotalRecordCount) throw new Error('Report changed during export. Refresh and retry.');
                    all = all.concat(result.Items); parameters.StartIndex += result.Items.length;
                    if (parameters.StartIndex < result.TotalRecordCount) return next();
                    var header = ['User', 'Title', 'Type', 'Year', 'Series', 'Episode', 'Status', 'Watched count', 'In-progress count', 'Available episodes', 'Saved play count', 'Last played UTC'];
                    var lines = [header.map(csvCell).join(',')];
                    all.forEach(function (row) { lines.push([row.UserName,row.Name,row.Type,row.Year,row.SeriesName,row.Episode,row.Status,row.WatchedCount,row.InProgressCount,row.AvailableEpisodeCount,row.SavedPlayCount,row.LastPlayedUtc].map(csvCell).join(',')); });
                    var url = URL.createObjectURL(new Blob(['\uFEFF' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' }));
                    var link = document.createElement('a'); link.href = url; link.download = 'WhoSceneIt-' + new Date().toISOString().slice(0,10) + '.csv'; document.body.appendChild(link); link.click(); link.remove(); setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
                    status('Exported ' + all.length + ' rows.');
                });
            }
            next().catch(function (error) { status(errorText(error), true); }).then(function () { state.exporting = false; find('export').disabled = !state.total; });
        }
        function renderUpdate(update) {
            if (!state.active) return;
            if (update.AwaitingDashboardVerification && state.reportHealthy && document.visibilityState !== 'hidden') {
                api('Updates/Healthy', {}, 'POST').catch(function () {});
            }
            text(find('updateHeading'), 'Who Scene It ' + update.InstalledVersion + (update.UpdateAvailable ? ' | Available: ' + update.LatestVersion : ''));
            text(find('updateMessage'), update.Message);
            var busy = update.Busy;
            find('checkUpdate').disabled = !!busy;
            find('downloadUpdate').hidden = !update.UpdateAvailable || update.Downloaded || update.RestartRequired;
            find('downloadUpdate').disabled = !!busy;
            find('installUpdate').hidden = !update.Downloaded || update.RestartRequired;
            find('installUpdate').disabled = !!busy;
            find('restartServer').hidden = !update.RestartRequired || !update.CanRestart;
            find('restartServer').disabled = !!busy;
            text(find('installUpdate'), update.CanRestart ? 'Install and restart' : 'Install update');
            find('restartWarning').hidden = !update.Downloaded || update.RestartRequired;
            text(find('restartWarning'), update.CanRestart ? 'Save open work. Installing this update restarts Emby Server and briefly disconnects its clients.' : 'Save open work. After installation, restart Emby Server using its normal controls.');
            find('releaseNotes').replaceChildren();
            (update.Notes || []).slice(0,3).forEach(function (note) { var li = document.createElement('li'); text(li, note); find('releaseNotes').appendChild(li); });
            find('releaseNotes').hidden = !update.UpdateAvailable || !(update.Notes || []).length;
            find('releaseLink').hidden = !update.UpdateAvailable || !update.HasMoreNotes;
            if (update.ReleaseUrl) find('releaseLink').href = update.ReleaseUrl;
            clearTimeout(pollTimer);
            pollTimer = setTimeout(pollUpdate, busy || update.RestartRequired ? 2000 : 30000);
        }
        function pollUpdate() {
            if (!state.active) return;
            api('Updates').then(renderUpdate).catch(function () { if (state.active) { text(find('updateMessage'), 'Emby Server is unreachable. Waiting to reconnect...'); pollTimer = setTimeout(pollUpdate, 3000); } });
        }
        function updateAction(action, reason) {
            find('checkUpdate').disabled = true; find('downloadUpdate').disabled = true; find('installUpdate').disabled = true;
            api('Updates/' + action, { Reason: reason || 'manual' }, 'POST').then(renderUpdate).catch(function (error) { text(find('updateMessage'), errorText(error)); pollTimer = setTimeout(pollUpdate, 2000); });
        }
        function focus() { if (state.active && document.visibilityState !== 'hidden') updateAction('Check', 'focus'); }
        function init() {
            if (state.initialized) { loadReport(); pollUpdate(); return; }
            state.initialized = true;
            find('userTab').addEventListener('click', function () { changeMode('user'); });
            find('titleTab').addEventListener('click', function () { changeMode('title'); });
            ['userSelect','viewSelect','sort','includeProgress'].forEach(function (id) { find(id).addEventListener('change', function () { state.start = 0; loadReport(); }); });
            find('filter').addEventListener('input', function () { clearTimeout(filterTimer); filterTimer = setTimeout(function () { state.start = 0; loadReport(); }, 300); });
            find('titleSearchForm').addEventListener('submit', function (event) { event.preventDefault(); searchTitles(0); });
            find('titlePrev').addEventListener('click', function () { searchTitles(Math.max(0,state.titleStart-30)); });
            find('titleNext').addEventListener('click', function () { searchTitles(state.titleStart+30); });
            find('refresh').addEventListener('click', function () { loadReport(); });
            find('prev').addEventListener('click', function () { state.start = Math.max(0,state.start-pageSize); loadReport(); });
            find('next').addEventListener('click', function () { state.start += pageSize; loadReport(); });
            find('export').addEventListener('click', exportCsv);
            find('checkUpdate').addEventListener('click', function () { updateAction('Check','manual'); });
            find('downloadUpdate').addEventListener('click', function () { updateAction('Download'); });
            find('installUpdate').addEventListener('click', function () { updateAction('Install'); });
            find('restartServer').addEventListener('click', function () { updateAction('Restart'); });
            api('Users').then(function (users) {
                find('reportApp').hidden = false; find('adminError').hidden = true;
                users.forEach(function (user) { var option = document.createElement('option'); option.value = user.Id; text(option,user.Name + (user.IsDisabled ? ' (disabled)' : '')); find('userSelect').appendChild(option); });
                loadReport();
            }).catch(function () { find('adminError').hidden = false; find('reportApp').hidden = true; });
            pollUpdate();
        }
        view.addEventListener('viewshow', function () { state.active = true; init(); window.addEventListener('focus',focus); document.addEventListener('visibilitychange',focus); focus(); });
        view.addEventListener('viewhide', function () { state.active = false; state.token++; state.titleToken++; clearTimeout(pollTimer); clearTimeout(filterTimer); window.removeEventListener('focus',focus); document.removeEventListener('visibilitychange',focus); });
    };
});
