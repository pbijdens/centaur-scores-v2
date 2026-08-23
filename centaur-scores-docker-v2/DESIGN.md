# Docker compose for CentaurScores V2

This document describes the design for the docker containers for V2 of the CentaurScores application

## Components

The application consists of the following components:

- In [../centaur-scores-api-v2](../centaur-scores-api-v2)
    - A .NET 10 Web API project
    - Needs a MySQL connection string configured via the environment at `ConnectionStrings__Default` in the format "server=LOCAL-SERVERNAME;port=LOCAL-PORT;database=${MYSQL_DATABASENAME};user=${MYSQL_USERNAME_APP};password=${MYSQL_PASSWORD_APP}"
    - The local deployment URL is configured via ASPNETCORE_URLS 
    - Writes all its logs, using logrotate with a maximum log size of 50MB per log file and a retention of 14 days, to a volume located locally on ${VOLUMES_LOGS}
    - Hosted in the virtual folder ${PUBLIC_API_VDIR}
- In [../centaur-scores-web-ui](../centaur-scores-web-ui) is
    - A Svelte application that can be built using "npm run build" and that reads its API URL via the build-time environment via VITE_API_BASE_URL=${PUBLIC_API_URL}
- In [../centaur-scores-mobile-web-scoring](../centaur-scores-mobile-web-scoring)
  - A Svelte application that can be built using "npm run build" and that needs no configuration other than the knowledge that it's hosted in the virtual folder ${PUBLIC_APP_VDIR}

## Dependencies

- MySql
  - The application needs a mysql database. The database should be accessible only from inside the compose environment. In the .env file the users and passwords are defined in 
    - MYSQL_USERNAME_ROOT / MYSQL_PASSWORD_ROOT
      For the root username and password to be used
    - MYSQL_USERNAME_APP / MYSQL_PASSWORD_APP
      For the user that should be created for the application
    - MYSQL_DATABASENAME
      For the databasename of the application database
    - The MYSQL instance should write all its data to ${VOLUMES_MYSQL}
- nginx
    - One service to rule them all and in darkness bind them
      - Will expose one single endpoint on ${NGINX_PUBLIC_PORT}
      - Directly on that port will host the `centaur-scores-web-ui`
      - With the centaur-scores-api-v2 reachable via the ${PUBLIC_API_VDIR} path, requests to that path are mapped to the centaur-scores-api-v2 deployment
      - With the centaur-scores-mobile-web-scoring available at ${PUBLIC_APP_VDIR}

## Summary

This docker container uses two local folders as volumes for MySQL data and Logs. It builds all software it runs from source, the build is performed inside the container. The container exposes one single port at NGINX_PUBLIC_PORT with on it two virtual paths, one for the API and one for the Scoring app. All other routes go to the webui application.

