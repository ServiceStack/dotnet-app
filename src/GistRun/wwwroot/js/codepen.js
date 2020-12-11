!function (e) {
    var t = {};

    function r(n) {
        if (t[n]) return t[n].exports;
        var i = t[n] = {i: n, l: !1, exports: {}};
        return e[n].call(i.exports, i, i.exports, r), i.l = !0, i.exports
    }

    r.m = e, r.c = t, r.d = function (e, t, n) {
        r.o(e, t) || Object.defineProperty(e, t, {enumerable: !0, get: n})
    }, r.r = function (e) {
        "undefined" != typeof Symbol && Symbol.toStringTag && Object.defineProperty(e, Symbol.toStringTag, {value: "Module"}), Object.defineProperty(e, "__esModule", {value: !0})
    }, r.t = function (e, t) {
        if (1 & t && (e = r(e)), 8 & t) return e;
        if (4 & t && "object" == typeof e && e && e.__esModule) return e;
        var n = Object.create(null);
        if (r.r(n), Object.defineProperty(n, "default", {
            enumerable: !0,
            value: e
        }), 2 & t && "string" != typeof e) for (var i in e) r.d(n, i, function (t) {
            return e[t]
        }.bind(null, i));
        return n
    }, r.n = function (e) {
        var t = e && e.__esModule ? function () {
            return e.default
        } : function () {
            return e
        };
        return r.d(t, "a", t), t
    }, r.o = function (e, t) {
        return Object.prototype.hasOwnProperty.call(e, t)
    }, r.p = "https://cpwebassets.codepen.io/assets/packs/", r(r.s = 565)
}({
    565: function (e, t, r) {
        "use strict";
        r.r(t);
        var n = {
            _HTML_TYPES: ["html", "xml", "haml", "markdown", "slim", "pug", "application/x-slim"],
            _CSS_TYPES: ["css", "less", "scss", "sass", "stylus", "postcss", "text/css", "text/x-sass", "text/x-scss", "text/x-less", "text/x-styl"],
            _JS_TYPES: ["js", "javascript", "coffeescript", "livescript", "typescript", "babel", "text/javascript", "text/x-coffeescript", "text/x-livescript", "text/typescript"],
            _CUSTOM_EDITOR_TYPES: {vue: "js", flutter: "js"},
            cmModeToType: function (e) {
                var t = this._getSafeInputMode(e);
                return this._getType(t)
            },
            _getSafeInputMode: function (e) {
                return ("string" == typeof e ? e : e.name).toLowerCase()
            },
            syntaxToType: function (e) {
                return this._getType(e)
            },
            _getType: function (e) {
                return -1 !== this._HTML_TYPES.indexOf(e) 
                    ? "html" : -1 !== this._CSS_TYPES.indexOf(e) 
                        ? "css" : -1 !== this._JS_TYPES.indexOf(e) 
                            ? "js" : this._CUSTOM_EDITOR_TYPES[e] 
                                ? this._CUSTOM_EDITOR_TYPES[e] 
                                : "unknown"
            }
        }, i = function e(t) {
            "loading" === document.readyState ? setTimeout((function () {
                e(t)
            }), 9) : t()
        }, a = ["title", "description", "tags", "html_classes", "head", "stylesheets", "scripts"], o = function (e) {
            for (var t = {}, r = e.attributes, n = 0, i = r.length; n < i; n++) {
                var a = r[n].name;
                0 === a.indexOf("data-") && (t[a.replace("data-", "")] = r[n].value)
            }
            return t = l(t), u(t) ? (t.user = s(t, e), t) : null
        }, s = function (e, t) {
            if ("string" == typeof e.user) return e.user;
            for (var r = 0, n = t.children.length; r < n; r++) {
                var i = (t.children[r].href || "").match(/codepen\.(io|dev)\/(\w+)\/pen\//i);
                if (i) return i[2]
            }
            return "anon"
        }, u = function (e) {
            return "prefill" in e || e["slug-hash"]
        }, l = function (e) {
            return e.href && (e["slug-hash"] = e.href), e.type && (e["default-tab"] = e.type), e.safe && ("true" === e.safe ? e.animations = "run" : e.animations = "stop-after-5"), e
        }, c = function (e) {
            var t = p(e), r = e.preview && "true" === e.preview ? "embed/preview" : "embed";
            if ("prefill" in e) return [t, r, "prefill"].join("/");
            var n = f(e);
            return [t, e.user ? e.user : "anon", r, e["slug-hash"] + "?" + n].join("/").replace(/\/\//g, "//")
        }, p = function (e) {
            return e.host ? d(e.host) : "https://codepen.io"
        }, d = function (e) {
            return e.match(/^\/\//) || !e.match(/https?:/) ? document.location.protocol + "//" + e : e
        }, f = function (e) {
            var t = "";
            for (var r in e) "prefill" !== r && ("" !== t && (t += "&"), t += r + "=" + encodeURIComponent(e[r]));
            return t
        }, m = function (e) {
            return e.height ? e.height : 300
        }, h = function (e, t) {
            var r, n = document.createDocumentFragment();
            n.appendChild(y(e)), "prefill" in e && (r = v(e, t), n.appendChild(r)), g(t, n), r && r.submit()
        }, _ = function (e, t) {
            var r = document.createElement(e);
            for (var n in t) Object.prototype.hasOwnProperty.call(t, n) && r.setAttribute(n, t[n]);
            return r
        }, v = function (e, t) {
            var r = _("form", {
                class: "cp_embed_form",
                style: "display: none;",
                method: "post",
                action: c(e),
                target: e.name
            });
            for (var i in e.data = function (e) {
                if (e.hasAttribute("data-prefill")) {
                    var t = {}, r = e.getAttribute("data-prefill");
                    for (var i in r = JSON.parse(decodeURI(r) || "{}")) a.indexOf(i) > -1 && (t[i] = r[i]);
                    for (var o = e.querySelectorAll("[data-lang]"), s = 0; s < o.length; s++) {
                        var u = o[s], l = u.getAttribute("data-lang");
                        u.getAttribute("data-options-autoprefixer") && (t.css_prefix = "autoprefixer");
                        var c = n.syntaxToType(l);
                        t[c] = u.innerText, l !== c && (t[c + "_pre_processor"] = l);
                        var p = u.getAttribute("data-lang-version");
                        p && (t[c + "_version"] = p)
                    }
                    return JSON.stringify(t)
                }
            }(t), e) "prefill" !== i && r.appendChild(_("input", {type: "hidden", name: i, value: e[i]}));
            return r
        }, y = function (e) {
            var t, r = c(e);
            t = e["pen-title"] ? e["pen-title"] : "CodePen Embed";
            var n = {
                allowfullscreen: "true",
                allowpaymentrequest: "true",
                allowTransparency: "true",
                class: "cp_embed_iframe " + (e.class ? e.class : ""),
                frameborder: "0",
                height: m(e),
                width: "100%",
                name: e.name || "CodePen Embed",
                scrolling: "no",
                src: r,
                style: "width: 100%; overflow:hidden; display:block;",
                title: t
            };
            return "prefill" in e == !1 && (n.loading = "lazy"), e["slug-hash"] && (n.id = "cp_embed_" + e["slug-hash"].replace("/", "_")), _("iframe", n)
        }, g = function (e, t) {
            if (e.parentNode) {
                var r = document.createElement("div");
                return r.className = "cp_embed_wrapper", r.appendChild(t), e.parentNode.replaceChild(r, e), r
            }
            return e.appendChild(t), e
        };
        var b = 1;

        function x(e) {
            e = "string" == typeof e ? e : ".codepen";
            for (var t = document.querySelectorAll(e), r = 0, n = t.length; r < n; r++) {
                var i = t[r], a = o(i);
                a && (a.name = "cp_embed_" + b++, h(a, i))
            }
            "function" == typeof __CodePenIFrameAddedToPage && __CodePenIFrameAddedToPage()
        }

        x.readme = "2019-01-18 - added version number back in.", window.__cp_eijs_version = "3.1.0", window.__cp_domReady = i, window.__CPEmbed = x, i(x)
    }
});