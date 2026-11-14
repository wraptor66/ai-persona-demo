const API_BASE = '/api/Report';

function loadSummary() {
    return fetch(`${API_BASE}/summary`).then(r => r.text());
}
