#!/bin/bash

# set environment variables used in deploy.sh and AWS task-definition.json:
export IMAGE_NAME=netcoreapps-rockwind-aws
export IMAGE_VERSION=latest

export AWS_DEFAULT_REGION=us-east-1
export AWS_ECS_CLUSTER_NAME=default
export AWS_VIRTUAL_HOST=rockwind-aws.web-app.io

# set any sensitive information in travis-ci encrypted project settings:
# required: AWS_ACCOUNT_ID, AWS_ACCESS_KEY, AWS_SECRET_KEY
# optional: SERVICESTACK_LICENSE