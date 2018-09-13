#!/bin/bash
source ./deploy-envs.sh

docker login $AZURE_REGISTRY -u $AZURE_REGISTRY_LOGIN -p $AZURE_REGISTRY_PASSWORD

docker push $AZURE_REGISTRY/$IMAGE_NAME:$IMAGE_VERSION
