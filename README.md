# Simple Web App

A minimal two-service web app for learning Docker and Kubernetes:

- **backend/** — ASP.NET Core minimal API (`/api/info`, `/api/heavy`, `/healthz`, `/health/{live,ready,startup}`). No database.
- **frontend/** — Angular app that calls the backend and displays which pod served the request.
- **k8s/** — Manifests for the app plus a few standalone learning examples:
  - `backend-deployment.yaml` — the backend Deployment; runs 4 replicas with a `RollingUpdate` strategy (`maxSurge: 1`, `maxUnavailable: 1`) and requires `amd64` nodes via `nodeAffinity`.
  - `frontend-deployment.yaml` — the frontend Deployment; uses `podAffinity` to require scheduling on the same node as a `backend` pod.
  - `backend-service.yaml`, `frontend-service.yaml` — Services for the core app.
  - `backend-configmap.yaml`, `backend-secret.yaml` — env vars injected into the backend via `envFrom`.
  - `backend-hpa.yaml` — HorizontalPodAutoscaler for the backend Deployment (needs `metrics-server`).
  - `ingress.yaml` — routes `/` to the frontend and `/api` to the backend (needs an ingress controller, e.g. ingress-nginx).
  - `cornJob.yaml` — a standalone CronJob demo (`my-job`) that runs daily at 08:00 and prints a message.
  - `node-exporter-daemonset.yaml` — a standalone DaemonSet demo running `node-exporter` on every node (`hostNetwork`/`hostPID`) in the `monitoring` namespace; the namespace must exist before applying.
  - `stateful-app.yaml`, `headless-service.yaml` — a standalone StatefulSet + headless Service demo in the `stateful-demo` namespace (unrelated to the frontend/backend app; the namespace must exist before applying).
- **argocd/** — an ArgoCD `Application` manifest (`application.yaml`) that points at this repo's `k8s/` folder for GitOps-style deployment.

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
2. Create the namespaces used by the standalone demos (only needed once):
   ```
   kubectl create namespace stateful-demo
   kubectl create namespace monitoring
   ```
3. Apply the manifests:
   ```
   kubectl apply -f k8s/
   ```
4. Check status:
   ```
   kubectl get pods
   kubectl get deployments
   kubectl get services
   ```
5. Open the app:
   - Docker Desktop / minikube with `nodePort`: http://localhost:30080 (Docker Desktop) or `http://$(minikube ip):30080` (minikube).
   - Otherwise: `kubectl port-forward svc/frontend-service 8081:80` then open http://localhost:8081.
   - Via Ingress (needs ingress-nginx installed): `kubectl apply -f k8s/ingress.yaml` then open http://localhost/.

## Deploy via ArgoCD (GitOps)

Instead of running `kubectl apply -f k8s/` by hand, ArgoCD can continuously sync the cluster to what's committed in this repo.

1. Install ArgoCD into its own namespace (only needed once per cluster):
   ```
   kubectl create namespace argocd
   kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
   ```
2. Access the ArgoCD UI/API (port-forward is simplest locally):
   ```
   kubectl port-forward svc/argocd-server -n argocd 8443:443
   ```
   Open https://localhost:8443. Log in as `admin`; get the initial password with:
   ```
   kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}"
   ```
   (decode the base64 output).
3. Register the app by applying the `Application` manifest in this repo:
   ```
   kubectl apply -f argocd/application.yaml
   ```
   This points ArgoCD at `k8s/` on the `main` branch, deploys into the `default` namespace, and turns on automated sync with `prune` + `selfHeal` — so any manifest change pushed to `main` is applied automatically, and manual `kubectl` drift gets reverted.
4. Check sync status:
   ```
   kubectl get application simple-web-app -n argocd
   argocd app get simple-web-app   # if the argocd CLI is installed
   ```

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
- Drive load against `/api/heavy` and watch `k8s/backend-hpa.yaml` scale the backend out (needs `metrics-server` installed):
  ```
  kubectl get hpa backend-hpa -w
  ```
- Look at the StatefulSet demo in `stateful-demo`: unlike the backend Deployment, pods get stable names and their own PersistentVolumeClaim:
  ```
  kubectl get pods -n stateful-demo -w
  kubectl get pvc -n stateful-demo
  kubectl delete pod stateful-app-0 -n stateful-demo
  ```
  The replacement pod comes back as `stateful-app-0` again and reattaches the same PVC — contrast with a Deployment pod, which gets a brand-new random name every time.
- Check the CronJob demo and the Jobs it spawns on schedule:
  ```
  kubectl get cronjob my-job -w
  kubectl get jobs --watch
  ```
- Confirm the `node-exporter` DaemonSet has exactly one pod per node:
  ```
  kubectl get pods -n monitoring -o wide
  kubectl get nodes
  ```
- Tear everything down:
  ```
  kubectl delete -f k8s/
  kubectl delete namespace stateful-demo
  kubectl delete namespace monitoring
  ```
