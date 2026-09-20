---
title: "CORS & the Angular proxy"
weight: 50
---

# CORS & the Angular proxy

The Angular dev server proxies API requests to the backend (`frontend/src/proxy.conf.ts`) instead of calling the API's origin directly. That sidesteps CORS entirely in development, since the browser only ever talks to the Angular dev server's own origin.

Further reading:

- [CORS in Aspire projects, and in general](https://medium.com/@gioboa/angulars-proxyconfig-unlock-a-senior-level-technique-used-by-only-10-of-developers-0c6730c5e1fd)
- [Angular's `proxyConfig` and how it relates to CORS](https://medium.com/@gioboa/angulars-proxyconfig-unlock-a-senior-level-technique-used-by-only-10-of-developers-0c6730c5e1fd)
