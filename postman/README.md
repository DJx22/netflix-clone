# Postman suite

Import these two files into Postman:

- `netflix-clone-api.postman_collection.json`
- `netflix-clone-local.postman_environment.json`

Select **Netflix Clone - Local**, then run the collection in order. The suite creates a unique account, captures tokens and IDs, tests all documented endpoints across Identity, Profile, Subscription, Payment, Catalog, and Streaming, and deletes the created profile in the final cleanup folder.

## Prerequisites

Start the six APIs with their `local` launch profiles and start the required infrastructure. Subscription activation after a successful payment also requires the RabbitMQ event path to be available.

Streaming has no media-asset creation endpoint. Before running the `07 - Streaming` folder, set the environment variable `streamingTitleId` to a title ID with a seeded `MediaAsset` row and blob. The collection still creates its own profile and uses that profile for playback-position tests.

## Run with Newman

```powershell
newman run .\postman\netflix-clone-api.postman_collection.json `
  -e .\postman\netflix-clone-local.postman_environment.json
```

To test only one service, run its numbered folder from the Postman UI. Identity must run before the authenticated service folders because it creates the bearer token used by the rest of the suite.
