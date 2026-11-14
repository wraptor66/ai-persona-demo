import { useEffect, useState } from 'react';

const TABS = {
  customers: {
    label: 'Customers',
    columns: [
      ['id', 'ID'],
      ['firstName', 'First'],
      ['lastName', 'Last'],
      ['email', 'Email'],
    ],
  },
  orders: {
    label: 'Orders',
    columns: [
      ['id', 'ID'],
      ['customerId', 'Customer'],
      ['orderDateUtc', 'Date', (v) => new Date(v).toLocaleDateString()],
      ['items', 'Lines', (v) => v.length],
      ['total', 'Total', (v) => `$${v.toFixed(2)}`],
    ],
  },
  products: {
    label: 'Products',
    columns: [
      ['sku', 'SKU'],
      ['name', 'Name'],
      ['category', 'Category'],
      ['price', 'Price', (v) => `$${v.toFixed(2)}`],
      ['stockQuantity', 'Stock'],
    ],
  },
};

export default function App() {
  const [tab, setTab] = useState('customers');
  const [rows, setRows] = useState([]);
  const [status, setStatus] = useState('loading');

  useEffect(() => {
    let cancelled = false;
    setStatus('loading');
    fetch(`/api/${tab}`)
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
      })
      .then((data) => {
        if (!cancelled) {
          setRows(data);
          setStatus('ok');
        }
      })
      .catch((e) => !cancelled && setStatus(`error: ${e.message}`));
    return () => {
      cancelled = true;
    };
  }, [tab]);

  const { columns } = TABS[tab];

  return (
    <main>
      <header>
        <h1>Northline Retail</h1>
        <nav>
          {Object.entries(TABS).map(([key, t]) => (
            <button key={key} className={key === tab ? 'active' : ''} onClick={() => setTab(key)}>
              {t.label}
            </button>
          ))}
        </nav>
      </header>
      {status !== 'ok' ? (
        <p className="status">{status}</p>
      ) : (
        <table>
          <thead>
            <tr>
              {columns.map(([, label]) => (
                <th key={label}>{label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id}>
                {columns.map(([field, label, fmt]) => (
                  <td key={label}>{fmt ? fmt(row[field]) : row[field]}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </main>
  );
}
