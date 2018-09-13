using System;
using System.IO;
using Funq;
using ServiceStack;
using ServiceStack.Text;
using ServiceStack.Redis;
using ServiceStack.OrmLite;

namespace NorthwindData
{
    public class Program
    {
        static string[] Providers = new[]{ "sqlite", "sqlserver", "mysql", "postgres", "redis" };

        public static OrmLiteConnectionFactory GetDbFactory(string dbProvider, string connectionString)
        {
            if (dbProvider == null || connectionString == null)
                return null;

            switch (dbProvider.ToLower())
            {
                case "sqlite":
                    var filePath = connectionString.MapProjectPath();
                    if (!File.Exists(filePath))
                        throw new FileNotFoundException($"SQLite database not found at '{filePath}'");
                    return new OrmLiteConnectionFactory(connectionString, SqliteDialect.Provider);
                case "mssql":
                case "sqlserver":
                    return new OrmLiteConnectionFactory(connectionString, SqlServerDialect.Provider);
                case "sqlserver2012":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2012Dialect.Provider);
                case "sqlserver2014":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2014Dialect.Provider);
                case "sqlserver2016":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2016Dialect.Provider);
                case "sqlserver2017":
                    return new OrmLiteConnectionFactory(connectionString, SqlServer2017Dialect.Provider);
                case "mysql":
                    return new OrmLiteConnectionFactory(connectionString, MySqlDialect.Provider);
                case "pgsql":
                case "postgres":
                case "postgresql":
                    PostgreSqlDialect.Provider.NamingStrategy = new OrmLiteNamingStrategyBase();
                    return new OrmLiteConnectionFactory(connectionString, PostgreSqlDialect.Provider);
            }

            return null;
        }

        public static string ResolveValue(string value)
        {
            if (value?.StartsWith("$") == true)
            {
                var envValue = Environment.GetEnvironmentVariable(value.Substring(1));
                if (!string.IsNullOrEmpty(envValue))
                    return envValue;
            }
            return value;
        }

        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                if (args.Length > 0)
                    ("Invalid Arguments: " + args.Join(" ")).Print();

                "Syntax:".Print();
                "northwind-data [provider] [connectionString]".Print();

                "\nAvailable Data Providers:".Print();
                Providers.Each(x => $" - {x}".Print());
                return;
            }

            var hasOrmLite = LicenseUtils.HasLicensedFeature(LicenseFeature.OrmLite);

            var dbFactory = new OrmLiteConnectionFactory("~/../../apps/northwind.sqlite".MapProjectPath(), SqliteDialect.Provider);
            var db = dbFactory.Open();
            var categories = db.Select<Category>();
            var customers = db.Select<Customer>();
            var employees = db.Select<Employee>();
            var orders = db.Select<Order>();
            var orderDetails = db.Select<OrderDetail>();
            var products = db.Select<Product>();
            var regions = db.Select<Region>();
            var shippers = db.Select<Shipper>();
            var suppliers = db.Select<Supplier>();
            var territories = db.Select<Territory>();

            var employeeTerritories = hasOrmLite ? db.Select<EmployeeTerritory>() : null;

            db.Dispose();

            var provider = ResolveValue(args[0]);
            var connectionString = ResolveValue(args[1]);

            dbFactory = GetDbFactory(provider, connectionString);
            if (dbFactory != null)
            {
                using (db = dbFactory.Open())
                {
                    db.DropAndCreateTable<Category>();
                    $"Created table {nameof(Category)}".Print();
                    db.DropAndCreateTable<Customer>();
                    $"Created table {nameof(Customer)}".Print();
                    db.DropAndCreateTable<Employee>();
                    $"Created table {nameof(Employee)}".Print();
                    db.DropAndCreateTable<Order>();
                    $"Created table {nameof(Order)}".Print();
                    db.DropAndCreateTable<OrderDetail>();
                    $"Created table {nameof(OrderDetail)}".Print();
                    db.DropAndCreateTable<Product>();
                    $"Created table {nameof(Product)}".Print();
                    db.DropAndCreateTable<Region>();
                    $"Created table {nameof(Region)}".Print();
                    db.DropAndCreateTable<Shipper>();
                    $"Created table {nameof(Shipper)}".Print();
                    db.DropAndCreateTable<Supplier>();
                    $"Created table {nameof(Supplier)}".Print();
                    db.DropAndCreateTable<Territory>();
                    $"Created table {nameof(Territory)}".Print();

                    if (hasOrmLite)
                    {
                        db.DropAndCreateTable<EmployeeTerritory>();
                        $"Created table {nameof(EmployeeTerritory)}".Print();
                    }

                    "".Print();

                    db.InsertAll(categories);
                    $"Inserted {categories.Count} rows in {nameof(Category)}".Print();
                    db.InsertAll(customers);
                    $"Inserted {customers.Count} rows in {nameof(Customer)}".Print();
                    db.InsertAll(employees);
                    $"Inserted {employees.Count} rows in {nameof(Employee)}".Print();
                    db.InsertAll(orders);
                    $"Inserted {orders.Count} rows in {nameof(Order)}".Print();
                    db.InsertAll(orderDetails);
                    $"Inserted {orderDetails.Count} rows in {nameof(OrderDetail)}".Print();
                    db.InsertAll(products);
                    $"Inserted {products.Count} rows in {nameof(Product)}".Print();
                    db.InsertAll(regions);
                    $"Inserted {regions.Count} rows in {nameof(Region)}".Print();
                    db.InsertAll(shippers);
                    $"Inserted {shippers.Count} rows in {nameof(Shipper)}".Print();
                    db.InsertAll(suppliers);
                    $"Inserted {suppliers.Count} rows in {nameof(Supplier)}".Print();
                    db.InsertAll(territories);
                    $"Inserted {territories.Count} rows in {nameof(Territory)}".Print();

                    if (hasOrmLite)
                    {
                        db.InsertAll(employeeTerritories);
                        $"Inserted {employeeTerritories.Count} rows in {nameof(EmployeeTerritory)}".Print();
                    }
                }
            }
            else if (provider == "redis")
            {
                var redisManager = new RedisManagerPool(connectionString);
                using (var redis = redisManager.GetClient())
                {
                    redis.StoreAll(categories);
                    redis.StoreAll(customers);
                    redis.StoreAll(employees);
                    redis.StoreAll(employeeTerritories);
                    redis.StoreAll(orders);
                    redis.StoreAll(orderDetails);
                    redis.StoreAll(products);
                    redis.StoreAll(regions);
                    redis.StoreAll(shippers);
                    redis.StoreAll(suppliers);
                    redis.StoreAll(territories);
                }
            }
            else
            {
                $"Unknown Provider: {provider}".Print();
                "Available Providers:".Print();
                Providers.Join(", ").Print();
            }
        }
    }
}
