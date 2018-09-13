#!/bin/bash

# set environment variables used in deploy.sh and AWS task-definition.json:
export IMAGE_NAME=netcoreapps-rockwind-azure
export IMAGE_VERSION=latest

export AZURE_REGISTRY=netcoreapps.azurecr.io

# set any sensitive information in travis-ci encrypted project settings:
# required: AZURE_REGISTRY_LOGIN, AZURE_REGISTRY_PASSWORD
# optional: SERVICESTACK_LICENSE
