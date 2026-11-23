# Museum Storage

The backend stores telemetry, environmental settings, and the exhibit registry. The React frontend uses the existing API to display readings and manage storage operations.

## Backend

Use the .NET 10 SDK and the EF Core CLI. Run these commands from the repository root.

```powershell
cd backend/MuseumStorage.Api
dotnet ef database update
dotnet run --launch-profile http
```

## Frontend

Use Node.js 20.19+ on the 20.x line or Node.js 22.12+. Open a second terminal at the repository root.

```powershell
npm --prefix frontend ci
npm --prefix frontend run dev
```

Open `http://localhost:5173` or the URL printed by Vite. Vite forwards `/api` requests to `http://localhost:5054`. To use a different backend, set `VITE_API_URL` in `frontend/.env.local` and restart Vite.

Run `npm --prefix frontend run build` to type-check and build the app. A static deployment needs an SPA fallback for page URLs and same-origin forwarding for `/api`.
