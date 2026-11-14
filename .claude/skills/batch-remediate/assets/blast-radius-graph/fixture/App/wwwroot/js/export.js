const EXPORT_BASE = '/api/Export';

function downloadCsv() {
    return fetch(`${EXPORT_BASE}/csv`);
}
