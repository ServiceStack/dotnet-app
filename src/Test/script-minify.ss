<!--
debug false
-->

Markdown:
{{#markdown}}
## Title

> quote

Paragraph with [a link](https://example.org).
{{/markdown}}

JS:
{{#minifyjs}}
function add(left, right) {
    return left + right;
}
add(1, 2);
{{/minifyjs}}


CSS:
{{#minifycss}}
body {
    background-color: #ffffff;
}
{{/minifycss}}
