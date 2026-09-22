# Vercel Deployment

The React frontend is planned for Vercel. The checked-in app retains the
generated Vite home surface but also includes the Auth session-management
workflow; it has not been deployed from this repository.

The frontend must receive the deployed ASP.NET Core API base URL through environment configuration.

Do not hard-code local addresses such as:

```text
http://localhost:8080
```

Production frontend configuration must point to the deployed API and use the actual CORS/trusted-origin configuration.
