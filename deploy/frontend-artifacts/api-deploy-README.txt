LorryOwner API — where the deployable build lives
===================================================

The API publish output is ~180 MB unpacked and ~76 MB zipped, which is past
GitHub's 100 MB per-file limit and would bloat the repo permanently, so unlike
the frontend package it is NOT committed here. It is built onto the machine
instead.

Build it
---------
From the repo root:

   .\deploy\publish-api.ps1

That writes a Release build to C:\TransTruckWeb\publish. It never touches
C:\TransTruckWeb\DB (the live database) or C:\TransTruckWeb\secrets (the JWT
signing key) — both live outside the publish folder for exactly this reason,
so republishing can never wipe either.

Zip it for copying to another machine
--------------------------------------
   Compress-Archive -Path C:\TransTruckWeb\publish\* `
       -DestinationPath C:\TransTruckWeb\TTAPI-<yyyyMMdd>.zip

On the target machine, unzip over <root>\publish, keeping that machine's own
DB\ and secrets\ folders untouched.

Run it
-------
   .\deploy\run-api.ps1          # Production, port 6041

Before the first run of a build carrying new migrations
---------------------------------------------------------
The API applies pending EF migrations at startup, against the live database.
It does take its own pre-upgrade backup first — but that backup is a plain
File.Copy of the .db and does NOT include the -wal sidecar, so anything still
in the write-ahead log at that moment is not in it.

Take a complete copy yourself first:

   $d = "C:\TransTruckWeb\DBBackup\manual-pre-deploy-$(Get-Date -Format yyyyMMdd-HHmmss)"
   New-Item -ItemType Directory -Force -Path $d
   Copy-Item C:\TransTruckWeb\DB\TransTruckWeb.db* -Destination $d

The .db, .db-wal and .db-shm together are the database. Copying only the .db
silently loses recent writes.
