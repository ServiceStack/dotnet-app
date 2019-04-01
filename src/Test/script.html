<!--
db sqlite
db.connection ~/../apps/northwind.sqlite
-->

<link rel="stylesheet" href="https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css">
<style>body {padding:1em} caption{caption-side:top;}</style>
<h1 class="py-2">Order Report #{{id}}</h1>

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
  {{ "table table-striped" | assignTo: className }}
  <style>table {border: 5px solid transparent} th {white-space: nowrap}</style>
  
  <div style="display:flex">
      {{ order | htmlDump({ caption: 'Order Details', className }) }}
      {{ `SELECT * FROM Customer WHERE Id = @CustomerId` 
         | dbSingle({ CustomerId }) | htmlDump({ caption: `Customer Details`, className }) }}
      {{ `SELECT Id, LastName, FirstName, Title, City, Country, Extension FROM Employee WHERE Id = @EmployeeId` 
         | dbSingle({ EmployeeId }) | htmlDump({ caption: `Employee Details`, className }) }}
  </div>

  {{ `SELECT p.ProductName, ${sqlCurrency("od.UnitPrice")} UnitPrice, Quantity, Discount
        FROM OrderDetail od
             INNER JOIN
             Product p ON od.ProductId = p.Id
       WHERE OrderId = @id`
      | dbSelect({ id }) 
      | htmlDump({ caption: "Line Items", className }) }}
{{else}}
  {{ `There is no Order with id: ${id}` }}
{{/with}}
