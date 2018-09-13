#!/bin/bash
source ../deploy-envs.sh

#AWS_ACCOUNT_ID={} set in private variable
export AWS_ECS_REPO_DOMAIN=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_DEFAULT_REGION.amazonaws.com

# Build process
docker build -t $IMAGE_NAME ../
docker tag $IMAGE_NAME $AWS_ECS_REPO_DOMAIN/$IMAGE_NAME:$IMAGE_VERSION

# Tag docker image for latter push to Azure Registry Container
docker tag $IMAGE_NAME $AZURE_REGISTRY/$IMAGE_NAME:$IMAGE_VERSION
