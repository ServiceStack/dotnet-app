#!/bin/bash
source ../deploy-envs.sh

export AWS_ECS_REPO_DOMAIN=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_DEFAULT_REGION.amazonaws.com
export ECS_SERVICE=$IMAGE_NAME-service
export ECS_TASK=$IMAGE_NAME-task

# install dependencies
sudo apt-get install jq -y #install jq for json parsing
sudo apt-get install gettext -y 
pip install --user awscli # install aws cli w/o sudo
export PATH=$PATH:$HOME/.local/bin # put aws in the path

# replace environment variables in task-definition
envsubst < task-definition.json > new-task-definition.json

export AWS_ACCESS_KEY_ID=$AWS_ACCESS_KEY
export AWS_SECRET_ACCESS_KEY=$AWS_SECRET_KEY

eval $(aws ecr get-login --region $AWS_DEFAULT_REGION) #needs AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY envvars

if [ $(aws ecr describe-repositories | jq --arg x $IMAGE_NAME '[.repositories[] | .repositoryName == $x] | any') == "true" ]; then
    echo "Found ECS Repository $IMAGE_NAME"
else
    echo "ECS Repository doesn't exist, Creating $IMAGE_NAME ..."
    aws ecr create-repository --repository-name $IMAGE_NAME
fi

docker push $AWS_ECS_REPO_DOMAIN/$IMAGE_NAME:$IMAGE_VERSION

aws ecs register-task-definition --cli-input-json file://new-task-definition.json --region $AWS_DEFAULT_REGION > /dev/null # Create a new task revision
TASK_REVISION=$(aws ecs describe-task-definition --task-definition $ECS_TASK --region $AWS_DEFAULT_REGION | jq '.taskDefinition.revision') #get latest revision
SERVICE_ARN="arn:aws:ecs:$AWS_DEFAULT_REGION:$AWS_ACCOUNT_ID:service/$ECS_SERVICE"
ECS_SERVICE_EXISTS=$(aws ecs list-services --region $AWS_DEFAULT_REGION --cluster $AWS_ECS_CLUSTER_NAME | jq '.serviceArns' | jq 'contains(["'"$SERVICE_ARN"'"])')
if [ "$ECS_SERVICE_EXISTS" == "true" ]; then
    echo "ECS Service already exists, Updating $ECS_SERVICE ..."
    aws ecs update-service --cluster $AWS_ECS_CLUSTER_NAME --service $ECS_SERVICE --task-definition "$ECS_TASK:$TASK_REVISION" --desired-count 1 --region $AWS_DEFAULT_REGION > /dev/null #update service with latest task revision
else
    echo "Creating ECS Service $ECS_SERVICE ..."
    aws ecs create-service --cluster $AWS_ECS_CLUSTER_NAME --service-name $ECS_SERVICE --task-definition "$ECS_TASK:$TASK_REVISION" --desired-count 1 --region $AWS_DEFAULT_REGION > /dev/null #create service
fi
if [ "$(aws ecs list-tasks --service-name $ECS_SERVICE --region $AWS_DEFAULT_REGION | jq '.taskArns' | jq 'length')" -gt "0" ]; then
    TEMP_ARN=$(aws ecs list-tasks --service-name $ECS_SERVICE --region $AWS_DEFAULT_REGION | jq '.taskArns[0]') # Get current running task ARN
    TASK_ARN="${TEMP_ARN%\"}" # strip double quotes
    TASK_ARN="${TASK_ARN#\"}" # strip double quotes
    aws ecs stop-task --task $TASK_ARN --region $AWS_DEFAULT_REGION > /dev/null # Stop current task to force start of new task revision with new image
fi
