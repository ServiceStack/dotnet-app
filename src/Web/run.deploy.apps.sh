cat ../apps/bare/web.settings | sed "/debug/s/ .*/ false/" > ../apps/web/web.bare.settings

cat ../apps/blog/web.release.settings > ../apps/web/web.blog.settings

cat ../apps/redis/web.settings | sed "/debug/s/ .*/ false/" > ../apps/web/web.redis.settings

cat ../apps/rockwind/web.sqlite.settings | sed "/debug/s/ .*/ false/" > ../apps/web/web.rockwind-sqlite.settings

cat ../apps/rockwind-vfs/web.sqlite.settings | sed "/debug/s/ .*/ false/" > ../apps/web/web.rockwind-vfs-sqlite.settings

cat ../apps/plugins/web.settings | sed "/debug/s/ .*/ false/" > ../apps/web/web.plugins.settings

cat ../apps/chat/web.release.settings > ../apps/web/web.chat.settings

cat ../apps/redis-html/web.settings | sed "/debug/s/ .*/ false/" > ../apps/web/web.redis-html.settings

rsync -avz -e 'ssh' ../apps deploy@web-app.io:/home/deploy

ssh deploy@web-app.io  "sudo supervisorctl restart all"
