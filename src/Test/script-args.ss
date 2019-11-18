
{{ ARGV |> dump }}

{{ dbTableNamesWithRowCounts |> textDump({ caption: 'Arg Tables' }) }}

{{ useDb({ connectionString:"Server=localhost;Database=test;UID=test;Password=test" }) }}
{{ dbTableNamesWithRowCounts() |> textDump({ caption: 'Test Tables' }) }}
