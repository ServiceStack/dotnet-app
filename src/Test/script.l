;<!--
; db sqlite
; db.connection ~/../apps/northwind.sqlite
;-->

(if (not (bound? id))
    (setq id 10692))

(setq order
    (/dbSingle "SELECT Id, OrderDate, CustomerId, Freight FROM [Order] o WHERE Id = @id" { :id id }) )

(if order
    (/textDump order { :caption "Order Details" })
    (println "There is no Order with id: " id) 
)
