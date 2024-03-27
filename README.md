# Problem this repo attempted to solve
We need to send logs from our mobile apps to a central log server for monitoring. It is essential that the mobile app send logs in a secured manner.

We uses Graylog as our log aggregator and monitoring.

The problem is Serilog graylog sink [Serilog.Sinks.Graylog](https://www.nuget.org/packages/Serilog.Sinks.Graylog) has no authentication mechanisin. This is probally due to the fact that graylog does not handle authentication and should not be exposed to a public end point.

# The solution
We will get the mobile app to send logs to our http server, which then forward to graylog.

[Serilog.Sinks.Http](https://www.nuget.org/packages/Serilog.Sinks.Http/) is an excellent choice.

## how do we add authentication to our endpoint?
We will use a key as part of the url. The serilog will be assigned a url like `https://example.com/serilog/my-key`. `my-key` must match a key in `appsettings.json` below
```
"Keys": {
    "cfbf4a08-644b-4d21-9772-aaaa6c28e071": "app1.example.com",
    "13778cb6-3418-4e96-b3e2-f991302ad1df": "app2.example.com",
    "927d8245-5b93-49b8-bcec-e127b58f6116": "app3.example.com"
  },
```

From a given key, we can tell which app it is.

Other actors not knowing our keys will not be able to abuse the endpoint.

# Integration
We uses docker compose to orchestrate the whole system consists of our http server, graylog, mongodb, and elastic search.

# build
```
cd docker-support
docker compose build
```

# run
```
cd docker-support
docker compose up -d
```

# Configure Graylog to receive GELF HTTP
1. Open graylog UI at `https://localhost:9000` and login
2. Navigate to System > Inputs
3. Add a GELF HTTP input