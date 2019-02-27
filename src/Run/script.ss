<!--
db sqlite
db.connection ~/../apps/northwind.sqlite
-->

{{ `SELECT o.Id, OrderDate, CustomerId, Freight, e.Id as EmployeeId, s.CompanyName as ShipVia, 
           ShipAddress, ShipCity, ShipPostalCode, ShipCountry
      FROM ${sqlQuote("Order")} o
           INNER JOIN
           Employee e ON o.EmployeeId = e.Id
           INNER JOIN
           Shipper s ON o.ShipVia = s.Id
     WHERE o.Id = @id` 
  | dbSingle({ id }) | assignTo: order }}

{{#with order}}

{{ order | textDump({ caption: 'Order Details' }) }}
{{ `SELECT * FROM Customer WHERE Id = @CustomerId` 
    | dbSingle({ CustomerId }) | textDump({ caption: `Customer Details` }) }}
{{ `SELECT Id, LastName, FirstName, Title, City, Country, Extension FROM Employee WHERE Id = @EmployeeId` 
    | dbSingle({ EmployeeId }) | textDump({ caption: `Employee Details` }) }}

{{ `SELECT p.ProductName, ${sqlCurrency("od.UnitPrice")} UnitPrice, Quantity, Discount
      FROM OrderDetail od
           INNER JOIN
           Product p ON od.ProductId = p.Id
     WHERE OrderId = @id`
    | dbSelect({ id }) 
    | textDump({ caption: "Line Items" }) }}

{{ `SELECT ${sqlCurrency("(od.UnitPrice * Quantity)")} AS OrderTotals 
      FROM OrderDetail od
           INNER JOIN
           Product p ON od.ProductId = p.Id
     WHERE OrderId = @id 
     ORDER BY 1 DESC`
    | dbSelect({ id }) 
    | textDump({ rowNumbers: false }) }}

{{else}}
  {{ `There is no Order with id: ${id}` }}
{{/with}}
