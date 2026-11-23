import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Link, NavLink, Route, Routes, useParams } from "react-router";
import { Dashboard, EnvironmentSettingsPage } from "./environment";
import { ExhibitDetail, ExhibitsPage } from "./exhibits";
import { StorageLocationsPage } from "./locations";
import "./styles.css";

const pages = [
  { path: "/", label: "Dashboard", icon: "dashboard" },
  { path: "/exhibits", label: "Exhibits", icon: "exhibits" },
  { path: "/storage-locations", label: "Storage locations", icon: "locations" },
  { path: "/environment-settings", label: "Environment settings", icon: "settings" },
];

function NotFound() {
  return (
    <>
      <h1>Page not found</h1>
      <p><Link to="/">Return to dashboard</Link></p>
    </>
  );
}

function ExhibitRoute() {
  const { id } = useParams();
  return id && /^\d+$/.test(id) ? <ExhibitDetail key={id} id={id} /> : <NotFound />;
}

function App() {
  return (
    <div className="app-layout">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <aside className="sidebar">
        <Link className="brand" to="/" aria-label="Museum Storage dashboard">
          <span className="brand-mark" aria-hidden="true"><span /></span>
          <span>Museum<strong>Storage</strong></span>
        </Link>
        <nav aria-label="Main navigation">
          {pages.map((page) => (
            <NavLink key={page.path} to={page.path} end={page.path === "/"}>
              <span className={`nav-icon icon-${page.icon}`} aria-hidden="true" />
              {page.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <main id="main-content" tabIndex={-1} className="workspace">
        <div className="workspace-content">
          <Routes>
            <Route path="/" element={<Dashboard />} />
            <Route path="/exhibits" element={<ExhibitsPage />} />
            <Route path="/exhibits/:id" element={<ExhibitRoute />} />
            <Route path="/storage-locations" element={<StorageLocationsPage />} />
            <Route path="/environment-settings" element={<EnvironmentSettingsPage />} />
            <Route path="*" element={<NotFound />} />
          </Routes>
        </div>
      </main>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
