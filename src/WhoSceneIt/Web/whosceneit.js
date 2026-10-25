define([], function () {
    'use strict';
    return function (view) {
        var find = function (name) { return view.querySelector('[data-id="' + name + '"]'); };
        var state = { mode: 'user', start: 0, total: 0, titleStart: 0, selected: null, series: null, token: 0, titleToken: 0, active: false, initialized: false, busy: false, exporting: false, reportHealthy: false };
        var pollTimer = null, filterTimer = null, deletionTimer = null;
        var selections = new Set(), selectionContext = '', currentRows = [];
        var deletion = { token: null, state: null, busy: false, submitting: false, ready: 0, request: 0, ids: [], userId: null };
        var pageSize = 100;
        function api(path, data, method) {
            return ApiClient.ajax({ type: method || 'GET', url: ApiClient.getUrl('WhoSceneIt/' + path, data || {}), dataType: 'json' }).catch(normalizeError);
        }
        function normalizeError(error) {
            if (!error || error.responseJSON || error.responseText || typeof error.text !== 'function') return Promise.reject(error);
            var response;
            try { response = typeof error.clone === 'function' ? error.clone() : error; } catch (_) { return Promise.reject(error); }
            return Promise.resolve().then(function () { return response.text(); }).then(function (body) {
                var normalized = { status: error.status, responseText: body };
                try { normalized.responseJSON = JSON.parse(body); } catch (_) {}
                return Promise.reject(normalized);
            }, function () { return Promise.reject(error); });
        }
        function text(node, value) { node.textContent = value == null ? '' : String(value); }
        function errorText(error) {
            if (error && (error.status === 401 || error.status === 403)) return 'Administrator access is required. Sign in again with an administrator account.';
            var result = error && error.responseJSON;
            if (!result && error && error.responseText) { try { result = JSON.parse(error.responseText); } catch (_) {} }
            var response = result && (result.ResponseStatus || result.responseStatus);
            var message = response && (response.Message || response.message) || result && (result.Message || result.message);
            var code = error && error.status ? ' (HTTP ' + error.status + ')' : '';
            if (typeof message === 'string' && message.trim()) return 'Emby request failed' + code + ': ' + message.trim().slice(0, 1200);
            return 'The request failed' + code + '. Check your connection and Emby Server logs, then try again.';
        }
        function status(message, error) { text(find('reportStatus'), message); find('reportStatus').classList.toggle('wsi-error', !!error); }
        function deletionApi(action, data, method) {
            if (method === 'GET') return api('Delete/' + action, data);
            return ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('WhoSceneIt/Delete/' + action), data: JSON.stringify(data || {}), contentType: 'application/json', dataType: 'json' }).catch(normalizeError);
        }
        function deletionError(error) {
            return errorText(error);
        }
        function updateSelections() {
            var byTitle = state.mode === 'title';
            find('viewDeletion').hidden = !deletion.token || deletion.state === 'Ready' || deletion.state === 'Loading' || deletion.state === 'PreviewFailed';
            text(find('viewDeletion'), deletion.busy ? 'View deletion progress' : 'View deletion results');
            text(find('selectionCount'), selections.size + ' selected');
            find('selectionCount').hidden = byTitle; find('clearSelection').hidden = byTitle;
            find('deleteSelected').hidden = byTitle; find('deleteTitle').hidden = !byTitle;
            find('clearSelection').disabled = !selections.size || deletion.busy;
            find('deleteSelected').disabled = !selections.size || state.busy || deletion.busy;
            find('deleteTitle').disabled = !state.selected || !currentRows.some(canDelete) || state.busy || deletion.busy;
            text(find('selectionNote'), byTitle ? 'Deletion includes completed media only. Saved partial playback and rewatches for any user are preserved. Review the exact paths before confirming.' : 'Select fully watched media individually or by page. Selections carry across pages. TV shows expand to completed episodes. Saved partial playback and rewatches for any user are preserved. Deleting media affects every user.');
            view.querySelectorAll('[data-item-selection]').forEach(function (box) { box.checked = selections.has(box.dataset.itemSelection); box.disabled = deletion.busy; });
            var pageBox = view.querySelector('[data-page-selection]');
            if (pageBox) {
                var eligible = currentRows.filter(canDelete);
                pageBox.checked = !!eligible.length && eligible.every(function (row) { return selections.has(row.ItemId); });
                pageBox.indeterminate = !pageBox.checked && eligible.some(function (row) { return selections.has(row.ItemId); });
                pageBox.disabled = deletion.busy || !eligible.length;
            }
        }
        function canDelete(row) { return row.DeletableCount != null ? row.DeletableCount > 0 : row.WatchedCount > 0 && !row.WatchedWithResumeCount; }
        function resetSelections() { selections.clear(); updateSelections(); }
        function reportParams(start, limit) {
            var values = { StartIndex: start, Limit: limit, WatchState: find('watchState').value, IncludeInProgress: true, SearchTerm: find('filter').value.trim(), SortBy: find('sort').value };
            if (state.mode === 'user') { values.UserId = find('userSelect').value; values.View = find('viewSelect').value; if (state.series) values.SeriesId = state.series.Id; }
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
            currentRows = []; updateSelections();
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
        function durationLabel(ticks) {
            if (ticks == null) return '';
            var seconds = Math.floor(Math.max(0, ticks) / 10000000), hours = Math.floor(seconds / 3600), minutes = Math.floor(seconds / 60) % 60;
            return (hours ? hours + ':' + String(minutes).padStart(2, '0') : String(minutes)) + ':' + String(seconds % 60).padStart(2, '0');
        }
        function progressCell(row, item) {
            var series = item.Type === 'Series', percent = series ? item.CompletionPercent : item.PlaybackPercent;
            var td = cell(row, percent == null ? (series ? 'Episode total unavailable' : (item.PlaybackPositionTicks > 0 ? 'Resume at ' + durationLabel(item.PlaybackPositionTicks) : 'Duration not recorded')) : percent + '%' + (series ? ' episodes completed' : (item.WatchedCount && !item.WatchedWithResumeCount ? ' complete' : ' saved position')));
            td.className = 'wsi-progress';
            if (percent != null) { var bar = document.createElement('progress'); bar.max = 100; bar.value = Math.max(0, Math.min(100, percent)); bar.setAttribute('aria-label', series ? 'Episodes completed' : 'Saved playback position'); td.appendChild(bar); }
            var detail = document.createElement('small');
            if (series) text(detail, (item.NotStartedCount == null ? '' : item.NotStartedCount + ' not started') + (item.WatchedWithResumeCount ? '; ' + item.WatchedWithResumeCount + ' watched with resume' : ''));
            else if (item.RunTimeTicks != null) text(detail, item.WatchedCount && !item.WatchedWithResumeCount ? 'Completed in Emby; ' + durationLabel(item.RunTimeTicks) + ' total' : durationLabel(item.PlaybackPositionTicks) + ' of ' + durationLabel(item.RunTimeTicks) + '; ' + durationLabel(item.RemainingTicks) + ' remaining');
            else text(detail, 'Duration not recorded; percentage and remaining time unavailable');
            td.appendChild(detail);
        }
        function episodeButton(parent, item, byTitle) {
            var detail = document.createElement('small'), button = document.createElement('button'); button.type = 'button'; button.className = 'wsi-link'; text(button, 'View episodes');
            button.setAttribute('aria-label', 'View episodes for ' + (byTitle ? item.UserName : item.Name));
            button.addEventListener('click', function () {
                state.series = byTitle ? { Id: state.selected.Id, Name: state.selected.Name } : { Id: item.ItemId, Name: item.Name };
                if (byTitle) find('userSelect').value = item.UserId;
                find('viewSelect').value = 'Episodes'; find('sort').value = 'Episode'; find('filter').value = '';
                text(find('episodeScopeName'), 'Episodes in ' + state.series.Name); find('episodeScope').hidden = false; changeMode('user');
            }); detail.appendChild(button); parent.appendChild(detail);
        }
        function renderReport(result) {
            var rows = result.Items || [], byTitle = state.mode === 'title';
            state.total = result.TotalRecordCount || 0;
            currentRows = rows;
            var headings = byTitle ? ['User', 'Watch status', 'Watched / partial', 'Progress', 'Saved play count', 'Last played'] : ['Title', 'Type', 'Watch status', 'Watched / partial', 'Progress', 'Saved play count', 'Last played'];
            var tr = document.createElement('tr');
            if (!byTitle) {
                var selectionHead = document.createElement('th'), pageBox = document.createElement('input');
                pageBox.type = 'checkbox'; pageBox.dataset.pageSelection = 'true'; pageBox.setAttribute('aria-label', 'Select watched titles on this page');
                pageBox.addEventListener('change', function () { currentRows.filter(canDelete).forEach(function (row) { if (pageBox.checked) selections.add(row.ItemId); else selections.delete(row.ItemId); }); updateSelections(); });
                selectionHead.appendChild(pageBox); tr.appendChild(selectionHead);
            }
            headings.forEach(function (label) { var th = document.createElement('th'); th.scope = 'col'; text(th, label); tr.appendChild(th); });
            if (!byTitle) { var actionHead = document.createElement('th'); actionHead.scope = 'col'; text(actionHead, 'Delete media'); tr.appendChild(actionHead); }
            find('reportHead').replaceChildren(tr); find('reportBody').replaceChildren();
            rows.forEach(function (item) {
                var row = document.createElement('tr');
                if (!byTitle) {
                    var selectionCell = cell(row, ''); selectionCell.className = 'wsi-selection';
                    if (canDelete(item)) {
                        var box = document.createElement('input'); box.type = 'checkbox'; box.dataset.itemSelection = item.ItemId;
                        box.setAttribute('aria-label', 'Select ' + item.Name);
                        box.addEventListener('change', function () { if (box.checked) selections.add(item.ItemId); else selections.delete(item.ItemId); updateSelections(); });
                        selectionCell.appendChild(box);
                    }
                }
                if (byTitle) { var userCell = cell(row, item.UserName, item.UserDisabled ? 'Disabled account' : ''); if (item.Type === 'Series') episodeButton(userCell, item, true); }
                else {
                    var nameCell = cell(row, '', item.Type === 'Episode' ? [item.SeriesName, item.Episode].filter(Boolean).join(' - ') : (item.Year || ''));
                    var titleButton = document.createElement('button'); titleButton.type = 'button'; titleButton.className = 'wsi-link'; text(titleButton, item.Name);
                    titleButton.addEventListener('click', function () { state.selected = { Id: item.ItemId, Name: item.Name, Type: item.Type, Year: item.Year }; showSelected(); changeMode('title'); });
                    nameCell.prepend(titleButton); if (item.Type === 'Series') episodeButton(nameCell, item, false); cell(row, item.Type === 'Series' ? 'TV show' : item.Type);
                }
                cell(row, item.Status);
                var count = item.WatchedCount + ' watched';
                if (item.Type === 'Series') count += ' episodes';
                if (item.AvailableEpisodeCount != null) count += ' of ' + item.AvailableEpisodeCount + ' available';
                if (item.InProgressCount) count += '; ' + item.InProgressCount + ' partially watched';
                cell(row, count); progressCell(row, item); cell(row, item.SavedPlayCount || 0); cell(row, dateLabel(item.LastPlayedUtc));
                if (!byTitle) {
                    var actionCell = cell(row, ''), remove = document.createElement('button'); remove.type = 'button'; remove.className = 'wsi-danger';
                    text(remove, item.Type === 'Series' ? 'Delete watched episodes...' : 'Delete...'); remove.disabled = !canDelete(item) || deletion.busy;
                    remove.setAttribute('aria-label', 'Review deletion of ' + item.Name);
                    remove.addEventListener('click', function () { previewDeletion([item.ItemId]); }); actionCell.appendChild(remove);
                }
                find('reportBody').appendChild(row);
            });
            find('tableWrap').hidden = !rows.length; find('pager').hidden = state.total <= pageSize;
            find('prev').disabled = state.start <= 0; find('next').disabled = state.start + rows.length >= state.total;
            text(find('pageInfo'), (state.start + 1) + '-' + (state.start + rows.length) + ' of ' + state.total);
            find('export').disabled = !state.total || state.exporting;
            status(state.total ? state.total + ' matching ' + (byTitle ? 'users' : 'titles') + '. Dates use your browser time zone.' : 'No matching results.');
            updateSelections();
        }
        function loadReport() {
            if (!state.active) return Promise.resolve();
            if (state.mode === 'title' && !state.selected) { clearReport(); status('Search for a title and select a result.'); return Promise.resolve(); }
            if (state.mode === 'user' && !find('userSelect').value) { clearReport(); status('No Emby users were found.'); return Promise.resolve(); }
            var token = ++state.token, path = state.mode === 'user' ? 'UserReport' : 'Viewers';
            var context = JSON.stringify([state.mode, find('userSelect').value, find('viewSelect').value, find('filter').value.trim(), find('watchState').value, state.series && state.series.Id, state.selected && state.selected.Id]);
            if (context !== selectionContext) { selectionContext = context; selections.clear(); }
            state.busy = true; state.reportHealthy = false; find('export').disabled = true; status('Loading report...');
            updateSelections();
            return api(path, reportParams(state.start, pageSize)).then(function (result) {
                if (token !== state.token || !state.active) return;
                state.busy = false;
                state.reportHealthy = true;
                if (!result.Items.length && state.start > 0 && result.TotalRecordCount > 0) { state.start = Math.floor((result.TotalRecordCount - 1) / pageSize) * pageSize; return loadReport(); }
                renderReport(result);
            }).catch(function (error) { if (token === state.token && state.active) { state.busy = false; state.reportHealthy = false; selections.clear(); clearReport(); status(errorText(error), true); } });
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
        function confirmEnabled() { find('confirmDelete').disabled = deletion.busy || deletion.state !== 'Ready' || !deletion.ready || !find('deleteAcknowledge').checked || find('deleteWord').value !== 'DELETE'; }
        function previewFailed(message) {
            deletion.state = 'PreviewFailed'; deletion.busy = false; deletion.ready = 0; deletion.token = null;
            text(find('deletePhase'), 'Preview failed. No deletion was started for this selection.');
            text(find('deleteSummary'), message);
            find('deleteConfirmFields').hidden = true; find('confirmDelete').hidden = true; find('cancelDelete').hidden = true;
            find('deleteContinuation').hidden = true; find('deleteProgressArea').hidden = true;
            find('closeDelete').hidden = false; find('retryPreview').hidden = false; updateSelections();
        }
        function showDeletion(result) {
            if (!result || result.State === 'PreviewFailed' || !result.Token) {
                if (deletion.busy) { deletionStatusUnavailable('Emby did not return a valid operation status.'); return; }
                previewFailed(result && result.Message || 'Emby did not return a valid deletion preview.'); return;
            }
            ['Deleted','Ready','Skipped','Failed','Total','Processing'].forEach(function (key) { if (typeof result[key] !== 'number') result[key] = 0; });
            deletion.token = result.Token; deletion.state = result.State; deletion.ready = result.Ready; deletion.busy = result.State === 'Deleting';
            var confirm = result.State === 'Ready' && result.Ready > 0;
            text(find('deletePhase'), result.State === 'Ready' ? (confirm ? 'Step 2 of 3: Review and confirm. No deletion has started.' : 'Nothing can be deleted. Review the blocked items below.') : result.State === 'Deleting' ? 'Step 3 of 3: Deleting confirmed media' : result.State === 'Cancelled' ? (result.Started === false ? 'Preview cancelled. No deletion started.' : 'Deletion cancelled or stopped') : (result.Failed || result.Skipped ? 'Deletion finished with items to review' : 'Deletion completed'));
            find('retryPreview').hidden = true; find('deleteContinuation').hidden = result.State !== 'Deleting';
            var processed = result.Processed == null ? (result.Deleted || 0) + (result.Skipped || 0) + (result.Failed || 0) : result.Processed;
            find('deleteProgressArea').hidden = result.State === 'Ready' || result.Started === false || !result.Total;
            find('deleteProgress').max = Math.max(1, result.Total || 0); find('deleteProgress').value = processed;
            text(find('deleteProgressText'), processed + ' of ' + result.Total + ' processed' + (result.CurrentItemName ? '. Current item: ' + result.CurrentItemName : '') + '. ' + (result.Processing || 0) + ' currently deleting.');
            text(find('deleteSummary'), result.Message + ' ' + result.Deleted + ' deleted; ' + result.Ready + ' ready; ' + result.Skipped + ' blocked or skipped; ' + result.Failed + ' failed.');
            find('deleteItems').replaceChildren();
            (result.Items || []).forEach(function (item) {
                var li = document.createElement('li'); text(li, item.Name + (item.Detail ? ' - ' + item.Detail : '') + ' (' + item.State + ')');
                var detail = document.createElement('small'); text(detail, (item.Paths || []).join('\n') + ((item.WatchedBy || []).length ? '\nWatched by: ' + item.WatchedBy.join(', ') : '') + (item.Message ? '\n' + item.Message : '')); li.appendChild(detail); find('deleteItems').appendChild(li);
            });
            find('deleteConfirmFields').hidden = !confirm; find('confirmDelete').hidden = !confirm;
            text(find('confirmDelete'), 'Permanently delete ' + result.Ready + ' media item' + (result.Ready === 1 ? '' : 's'));
            find('cancelDelete').hidden = result.State !== 'Ready' && result.State !== 'Deleting';
            find('closeDelete').hidden = result.State === 'Ready'; text(find('closeDelete'), deletion.busy ? 'Close progress view' : 'Close and refresh');
            find('cancelDelete').disabled = false; text(find('cancelDelete'), deletion.busy ? 'Stop after current item' : (confirm ? 'Cancel' : 'Close preview'));
            confirmEnabled(); updateSelections();
            clearTimeout(deletionTimer);
            if (deletion.busy && state.active) deletionTimer = setTimeout(pollDeletion, 1000);
        }
        function previewDeletion(ids, fixedUserId) {
            if (deletion.busy || !ids.length) return;
            if (ids.length > 500) { status('Select at most 500 titles per deletion batch.', true); return; }
            var request = ++deletion.request;
            deletion.ids = ids.slice(); deletion.userId = arguments.length > 1 ? fixedUserId : (state.mode === 'user' ? find('userSelect').value : null);
            deletion.token = null; deletion.state = 'Loading'; deletion.ready = 0;
            text(find('deletePhase'), 'Step 1 of 3: Preparing preview for ' + ids.length + ' selected title' + (ids.length === 1 ? '' : 's') + '. No deletion has started.');
            find('deleteContinuation').hidden = true; find('deleteProgressArea').hidden = true; find('retryPreview').hidden = true;
            find('deleteAcknowledge').checked = false; find('deleteWord').value = ''; find('deleteItems').replaceChildren();
            text(find('deleteSummary'), 'Checking watched items, Emby permissions, playback sessions, and deletion paths...');
            find('deleteConfirmFields').hidden = true; find('confirmDelete').hidden = true; find('cancelDelete').hidden = false;
            find('closeDelete').hidden = true; find('cancelDelete').disabled = false; text(find('cancelDelete'), 'Cancel');
            if (!find('deleteDialog').open) find('deleteDialog').showModal();
            deletionApi('Preview', { UserId: deletion.userId, ItemIds: ids.join(',') }).then(function (result) {
                if (request !== deletion.request || !state.active) { if (result && result.Token) deletionApi('Cancel', { Token: result.Token }).catch(function () {}); return; }
                showDeletion(result);
            }).catch(function (error) { if (request === deletion.request) previewFailed(deletionError(error)); });
        }
        function pollDeletion() {
            if (!state.active || !deletion.token || deletion.submitting) return;
            deletionApi('Status', { Token: deletion.token }, 'GET').then(showDeletion).catch(function (error) {
                deletionStatusUnavailable(deletionError(error));
            });
        }
        function deletionStatusUnavailable(message) {
            text(find('deletePhase'), 'Deletion status is unavailable. The confirmed operation may still be running.');
            text(find('deleteSummary'), message + ' Do not submit another deletion. Status will be checked again.');
            find('confirmDelete').hidden = true; find('deleteConfirmFields').hidden = true; find('retryPreview').hidden = true;
            find('cancelDelete').disabled = false; text(find('cancelDelete'), 'Stop remaining items');
            find('closeDelete').hidden = false; text(find('closeDelete'), 'Close progress view');
            updateSelections();
            if (state.active && deletion.token) { clearTimeout(deletionTimer); deletionTimer = setTimeout(pollDeletion, 3000); }
        }
        function executeDeletion() {
            if (find('confirmDelete').disabled) return;
            deletion.busy = true; deletion.submitting = true; deletion.state = 'Submitting'; confirmEnabled(); find('cancelDelete').disabled = true; updateSelections();
            text(find('deletePhase'), 'Step 3 of 3: Submitting confirmed deletion. Waiting for Emby...');
            find('deleteConfirmFields').hidden = true; find('confirmDelete').hidden = true; find('deleteContinuation').hidden = false;
            find('deleteProgressArea').hidden = false;
            find('closeDelete').hidden = false; text(find('closeDelete'), 'Close progress view');
            deletionApi('Execute', { Token: deletion.token, Confirmation: find('deleteWord').value }).then(function (result) { deletion.submitting = false; showDeletion(result); }).catch(function (error) {
                deletion.submitting = false;
                deletionStatusUnavailable(deletionError(error));
                // A lost response must never trigger another destructive request.
                pollDeletion();
            });
        }
        function cancelDeletion() {
            ++deletion.request;
            if (!deletion.token) { find('deleteDialog').close(); return; }
            var wasRunning = deletion.busy;
            find('cancelDelete').disabled = true;
            deletionApi('Cancel', { Token: deletion.token }).then(function (result) { showDeletion(result); if (!wasRunning) find('deleteDialog').close(); }).catch(function (error) { text(find('deleteSummary'), deletionError(error)); find('cancelDelete').disabled = false; });
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
                    var header = ['User', 'Title', 'Type', 'Year', 'Series', 'Episode', 'Status', 'Watched count', 'In-progress count', 'Available episodes', 'Saved play count', 'Last played UTC', 'Not-started episodes', 'Watched-with-resume count', 'Episode completion percent', 'Saved position', 'Duration', 'Remaining time', 'Playback percent'];
                    var lines = [header.map(csvCell).join(',')];
                    all.forEach(function (row) { lines.push([row.UserName,row.Name,row.Type,row.Year,row.SeriesName,row.Episode,row.Status,row.WatchedCount,row.InProgressCount,row.AvailableEpisodeCount,row.SavedPlayCount,row.LastPlayedUtc,row.NotStartedCount,row.WatchedWithResumeCount,row.CompletionPercent,durationLabel(row.PlaybackPositionTicks),durationLabel(row.RunTimeTicks),durationLabel(row.RemainingTicks),row.PlaybackPercent].map(csvCell).join(',')); });
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
            find('downloadUpdate').disabled = !!busy || deletion.busy;
            find('installUpdate').hidden = !update.Downloaded || update.RestartRequired;
            find('installUpdate').disabled = !!busy || deletion.busy;
            find('restartServer').hidden = !update.RestartRequired || !update.CanRestart;
            find('restartServer').disabled = !!busy || deletion.busy;
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
            if (deletion.busy && (action === 'Install' || action === 'Restart' || action === 'Download')) return;
            find('checkUpdate').disabled = true; find('downloadUpdate').disabled = true; find('installUpdate').disabled = true;
            api('Updates/' + action, { Reason: reason || 'manual' }, 'POST').then(renderUpdate).catch(function (error) { text(find('updateMessage'), errorText(error)); pollTimer = setTimeout(pollUpdate, 2000); });
        }
        function focus() { if (state.active && document.visibilityState !== 'hidden') updateAction('Check', 'focus'); }
        function init() {
            if (state.initialized) { loadReport(); pollUpdate(); return; }
            state.initialized = true;
            find('userTab').addEventListener('click', function () { changeMode('user'); });
            find('titleTab').addEventListener('click', function () { changeMode('title'); });
            ['userSelect','viewSelect','sort','watchState'].forEach(function (id) { find(id).addEventListener('change', function () { if (id === 'viewSelect' && find(id).value !== 'Episodes') { state.series = null; find('episodeScope').hidden = true; } state.start = 0; loadReport(); }); });
            find('clearSeries').addEventListener('click', function () { state.series = null; find('episodeScope').hidden = true; find('viewSelect').value = 'Shows'; state.start = 0; loadReport(); });
            find('filter').addEventListener('input', function () { clearTimeout(filterTimer); filterTimer = setTimeout(function () { state.start = 0; loadReport(); }, 300); });
            find('titleSearchForm').addEventListener('submit', function (event) { event.preventDefault(); searchTitles(0); });
            find('titlePrev').addEventListener('click', function () { searchTitles(Math.max(0,state.titleStart-30)); });
            find('titleNext').addEventListener('click', function () { searchTitles(state.titleStart+30); });
            find('refresh').addEventListener('click', function () { loadReport(); });
            find('prev').addEventListener('click', function () { state.start = Math.max(0,state.start-pageSize); loadReport(); });
            find('next').addEventListener('click', function () { state.start += pageSize; loadReport(); });
            find('export').addEventListener('click', exportCsv);
            find('clearSelection').addEventListener('click', resetSelections);
            find('deleteSelected').addEventListener('click', function () { previewDeletion(Array.from(selections)); });
            find('deleteTitle').addEventListener('click', function () { if (state.selected) previewDeletion([state.selected.Id]); });
            find('deleteAcknowledge').addEventListener('change', confirmEnabled); find('deleteWord').addEventListener('input', confirmEnabled);
            find('confirmDelete').addEventListener('click', executeDeletion); find('cancelDelete').addEventListener('click', cancelDeletion);
            find('retryPreview').addEventListener('click', function () { previewDeletion(deletion.ids, deletion.userId); });
            find('viewDeletion').addEventListener('click', function () { if (!find('deleteDialog').open) find('deleteDialog').showModal(); pollDeletion(); });
            find('closeDelete').addEventListener('click', function () { find('deleteDialog').close(); if (!deletion.busy) { resetSelections(); loadReport(); pollUpdate(); } });
            find('deleteDialog').addEventListener('cancel', function (event) { event.preventDefault(); if (!deletion.busy) cancelDeletion(); });
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
        view.addEventListener('viewshow', function () { state.active = true; init(); window.addEventListener('focus',focus); document.addEventListener('visibilitychange',focus); focus(); if (deletion.busy) { if (!find('deleteDialog').open) find('deleteDialog').showModal(); pollDeletion(); } });
        view.addEventListener('viewhide', function () {
            state.active = false; state.token++; state.titleToken++; deletion.request++;
            clearTimeout(pollTimer); clearTimeout(filterTimer); clearTimeout(deletionTimer);
            window.removeEventListener('focus',focus); document.removeEventListener('visibilitychange',focus);
            if (find('deleteDialog').open) find('deleteDialog').close();
            if (deletion.token && !deletion.busy && deletion.state === 'Ready') { deletionApi('Cancel', { Token: deletion.token }).catch(function () {}); deletion.state = 'Cancelled'; }
        });
    };
});
