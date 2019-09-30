"RSS Parser"

(def xml (/urlContentsWithCache "https://news.ycombinator.com/rss") )

(println (/subString xml 0 1000))

(defn parse-rss [xml]
    (let ( (to) (doc) (channel) (items) (el) )
        (def doc (System.Xml.Linq.XDocument/Parse xml))
        (def to  (ObjectDictionary.))
        (def items (List<ObjectDictionary>.))
        (def channel (first (.Descendants doc "channel")))
        (def el  (XLinqExtensions/FirstElement channel))

        (while (not= (.LocalName (.Name el)) "item")
            (.Add to (.LocalName (.Name el)) (.Value el))
            (def el (XLinqExtensions/NextElement el)))

        (doseq (elItem (.Descendants channel "item"))
            (def item (ObjectDictionary.))
            (def el (XLinqExtensions/FirstElement elItem))
            
            (while el
                (.Add item (.LocalName (.Name el)) (.Value el))
                (def el (XLinqExtensions/NextElement el)))
            
            (.Add items item))

        (.Add to "items" (to-list items))
        to
    )
)

(parse-rss xml)
