#!/bin/bash

# Get the newest file in nupkg directory
NEWEST=$(ls -t nupkg/* | head -n1)

# Push the package using dotnet nuget
dotnet nuget push "$NEWEST" -k $NUGET_APIKEY --source https://www.nuget.org/api/v2/package
