<!--
db sqlite
db.connection ~/../apps/northwind.sqlite
-->

(id ?? 10692) | to => id

`SELECT Id, OrderDate, CustomerId, Freight FROM "Order" o WHERE Id = @id` | dbSingle({ id }) | to => order

#with order
    order | textDump({ caption: 'Order Details' })
else
    `There is no Order with id: ${id}`
/with
