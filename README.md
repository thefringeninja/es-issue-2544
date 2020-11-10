# Issue 2544 Reproduction

First, run a single node
```bash
docker run --name=es-repro --rm -p 1113:1113 -p 2113:2113 \
       docker.pkg.github.com/eventstore/eventstore/eventstore:20.10.0-rc1-buster-slim \
       --insecure --enable-atom-pub-over-http
```
Then run the reproduction
```bash
 dotnet publish --configuration Release && \
        while true; do ./bin/Release/netcoreapp3.1/es-issue-2544; done
```
