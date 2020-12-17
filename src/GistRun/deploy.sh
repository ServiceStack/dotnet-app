#!/usr/bin/env bash
rsync -avz -e 'ssh' bin/Release/net5/publish/ deploy@gistlyn.com:/home/deploy/apps/gistrun
ssh deploy@gistlyn.com "sudo supervisorctl restart web-gistrun"
