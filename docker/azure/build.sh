#!/bin/bash
source ./deploy-envs.sh

# Have to copy to the root foolder to avoid docker context limitations
cp ./Dockerfile ../../Dockerfile
# Build process
docker build -t $IMAGE_NAME ../../.
# Tag docker image for latter push to Azure Registry Container
docker tag $IMAGE_NAME $AZURE_REGISTRY/$IMAGE_NAME:$IMAGE_VERSION
