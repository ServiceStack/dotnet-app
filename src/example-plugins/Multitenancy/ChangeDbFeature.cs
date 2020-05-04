using System;
using ServiceStack;

namespace Multitenancy
{
    public class ChangeDbFeature : IPlugin
    {
        public void Register(IAppHost appHost)
        {
            appHost.PreRequestFilters.Add((req, res) => {
                var db = req.QueryString["db"];
                if (db == null) return;
                req.Items[Keywords.DbInfo] = db == "northwind"
                    ? new ConnectionInfo { ConnectionString = "northwind.sqlite", ProviderName = "sqlite" }
                    : db == "techstacks"
                        ? new ConnectionInfo {
                            ConnectionString = Environment.GetEnvironmentVariable("TECHSTACKS_DB"),
                            ProviderName = "postgres"
                        }
                        : null;
            });
        }
    }
}