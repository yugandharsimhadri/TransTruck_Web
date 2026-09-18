LorryOwner API - where the deployable build lives
===================================================

Unlike the frontend ZIP beside this file, the API build is NOT committed:
it is ~180 MB unpacked, past GitHub's 100 MB per-file limit, and would bloat
this public repo permanently. It is rebuilt from source whenever needed.

Build it
---------
From the repo root, on the dev machine:

   dotnet publish src\TransTrack.Api\TransTrack.Api.csproj -c Release -o C:\TransTruckWeb-Postgres\publish

Deploy it
----------
Production runs at C:\server\loapi on the production server, on PostgreSQL,
configured entirely by the appsettings.json in that folder (connection
string, JWT signing key, CORS origins, port 6041).

The full routine - publish, copy over WITHOUT appsettings.json, restart,
verify - is in ..\..\DEPLOYMENT.md. The one rule worth repeating here: the
published folder's appsettings.json holds placeholders, and copying it over
the server's would replace the real connection string and key. Exclude it:

   robocopy C:\TransTruckWeb-Postgres\publish C:\server\loapi /E /XF appsettings.json

The one-time move from SQLite to PostgreSQL (done 2026-09-18) is in
..\..\POSTGRES-MIGRATION.md and is not part of a normal deploy.
