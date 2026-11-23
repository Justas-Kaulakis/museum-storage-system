import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Link, NavLink, Route, Routes, useParams } from "react-router";
import { EnvironmentPage } from "./environment";
import { ExhibitDetail, RegistryPage } from "./exhibits";
import "./styles.css";

function NotFound() {
  return (
    <>
      <h1>Page not found</h1>
      <p><Link to="/">Return to Environment</Link></p>
    </>
  );
}

function ExhibitRoute() {
  const { id } = useParams();
  return id && /^\d+$/.test(id) ? <ExhibitDetail key={id} id={id} /> : <NotFound />;
}

function App() {
  return (
    <>
      <a className="skip-link" href="#main-content">Skip to content</a>
      <header className="site-header">
        <div className="header-content">
          <Link className="brand" to="/">Museum Storage</Link>
          <nav aria-label="Main navigation">
            <NavLink to="/" end>Environment</NavLink>
            <NavLink to="/exhibits">Registry</NavLink>
          </nav>
        </div>
      </header>
      <main id="main-content" tabIndex={-1} className="workspace">
        <Routes>
          <Route path="/" element={<EnvironmentPage />} />
          <Route path="/exhibits" element={<RegistryPage />} />
          <Route path="/exhibits/:id" element={<ExhibitRoute />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>
    </>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
