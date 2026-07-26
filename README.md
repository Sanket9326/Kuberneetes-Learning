# Simple Web App

A minimal two-service web app for learning Docker and Kubernetes:

- **backend/** — ASP.NET Core minimal API (`/api/info`, `/healthz`). No database.
- **frontend/** — Angular app that calls the backend and displays which pod served the request.
- **k8s/** — Deployment + Service manifests for both services.

```
Browser -> frontend Service (NodePort) -> frontend pods (nginx + Angular)
                                              |
                                              v  (nginx reverse-proxies /api)
                                        backend Service (ClusterIP) -> backend pods (ASP.NET Core)
```

## Run locally without Docker

Terminal 1 (backend, listens on http://localhost:8080):

```
cd backend
dotnet run --urls http://localhost:8080
```

Terminal 2 (frontend, dev server proxies /api to the backend via proxy.conf.json):

```
cd frontend
npm start -- --proxy-config proxy.conf.json
```

Open http://localhost:4200.

## Build and run with Docker

Requires Docker Desktop running.

```
docker build -t simple-web-app-backend:latest ./backend
docker build -t simple-web-app-frontend:latest ./frontend

docker network create simple-web-app-net

docker run -d --name backend-service --network simple-web-app-net -p 8080:8080 simple-web-app-backend:latest
docker run -d --name frontend --network simple-web-app-net -p 8081:8080 simple-web-app-frontend:latest
```

Open http://localhost:8081. (The frontend container's nginx proxies `/api` to `backend-service:80` — that hostname only resolves on the shared Docker network, which is also why the container is named `backend-service` here.)

## Deploy to Kubernetes

Any local cluster works: Docker Desktop's built-in Kubernetes, `kind`, or `minikube`. Examples below assume Docker Desktop Kubernetes (images built locally are already visible to it — no registry push needed). If you use `kind`, run `kind load docker-image simple-web-app-backend:latest simple-web-app-frontend:latest` after building. If you use `minikube`, run `eval $(minikube docker-env)` before building the images so they land in minikube's Docker daemon.

1. Build the images (see above), tagged `simple-web-app-backend:latest` and `simple-web-app-frontend:latest` to match `k8s/*-deployment.yaml`.
2. Apply the manifests:
   ```
   kubectl apply -f k8s/
   ```
3. Check status:
   ```
   kubectl get pods
   kubectl get deployments
   kubectl get services
   ```
4. Open the app:
   - Docker Desktop / minikube with `nodePort`: http://localhost:30080 (Docker Desktop) or `http://$(minikube ip):30080` (minikube).
   - Otherwise: `kubectl port-forward svc/frontend-service 8081:80` then open http://localhost:8081.

## Things to try while learning Kubernetes

- Scale the backend and refresh the page to see the host name change as the Service load-balances across pods:
  ```
  kubectl scale deployment backend --replicas=5
  ```
- Delete a pod and watch Kubernetes recreate it automatically:
  ```
  kubectl get pods
  kubectl delete pod <backend-pod-name>
  kubectl get pods -w
  ```
- Watch a rolling update after changing and rebuilding the backend image:
  ```
  kubectl rollout restart deployment backend
  kubectl rollout status deployment backend
  ```
- Inspect the readiness/liveness probes in `k8s/backend-deployment.yaml` and `k8s/frontend-deployment.yaml`, and see what happens if `/healthz` starts failing.
- Tear everything down:
  ```
  kubectl delete -f k8s/
  ```
