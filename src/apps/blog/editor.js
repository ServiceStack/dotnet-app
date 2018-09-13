function Editor($editor, opt) {
  let history = [];
  let redo = [];

  let ops = {
    lang: opt.lang || "",
    target: $editor.querySelector("textarea"),
    $emit(evt, arg) {
      // input or save
      if (evt === "input") {
        this.target.value = arg;
        var event = new Event("input", {
          bubbles: true,
          cancelable: true
        });
        this.target.dispatchEvent(event);
      }
      if (opt[evt]) {
        opt[evt].call(this,arg);
      }
    },
    $nextTick(fn) {
      setTimeout(fn, 0);
    },
    input() {
      return this.target;
    },
    hasSelection() {
      return this.input().selectionStart !== this.input().selectionEnd;
    },
    selection() {
      let $txt = this.input();
      return $txt.value.substring($txt.selectionStart, $txt.selectionEnd) || "";
    },
    selectionInfo() {
      let $txt = this.input();
      let value = $txt.value,
        selPos = $txt.selectionStart,
        sel = value.substring(selPos, $txt.selectionEnd) || "",
        beforeSel = value.substring(0, selPos),
        prevCRPos = beforeSel.lastIndexOf("\n");
      return {
        value,
        sel,
        selPos,
        beforeSel,
        afterSel: value.substring(selPos),
        prevCRPos,
        beforeCR: prevCRPos >= 0 ? beforeSel.substring(0, prevCRPos + 1) : "",
        afterCR: prevCRPos >= 0 ? beforeSel.substring(prevCRPos + 1) : ""
      };
    },
    replace({ value, selectionStart, selectionEnd }) {
      if (selectionEnd == null) {
        selectionEnd = selectionStart;
      }
      let $txt = this.input();
      this.$emit("input", value);
      this.$nextTick(() => {
        $txt.focus();
        $txt.setSelectionRange(selectionStart, selectionEnd);
      });
    },
    insert(
      prefix,
      suffix,
      placeholder,
      {
        selectionAtEnd,
        offsetStart,
        offsetEnd,
        filterValue,
        filterSelection
      } = {}
    ) {
      let $txt = this.input();
      let value = $txt.value;
      let pos = $txt.selectionEnd;
      history.push({
        value,
        selectionStart: $txt.selectionStart,
        selectionEnd: $txt.selectionEnd
      });
      redo = [];
      let from = $txt.selectionStart,
        to = $txt.selectionEnd,
        len = to - from;
      let beforeRange = value.substring(0, from);
      let afterRange = value.substring(to);
      let toggleOff =
        prefix && beforeRange.endsWith(prefix) && afterRange.startsWith(suffix);
      let originalPos = pos;
      let noSelection = from == to;
      if (noSelection) {
        if (!toggleOff) {
          value = beforeRange + prefix + placeholder + suffix + afterRange;
          pos += prefix.length;
          offsetStart = 0;
          offsetEnd = placeholder.length;
          if (selectionAtEnd) {
            pos += offsetEnd;
            offsetEnd = 0;
          }
        } else {
          value =
            beforeRange.substring(0, beforeRange.length - prefix.length) +
            afterRange.substring(suffix.length);
          pos += -suffix.length;
        }
        if (filterValue) {
          var opt = { pos };
          value = filterValue(value, opt);
          pos = opt.pos;
        }
      } else {
        var selectedText = value.substring(from, to);
        if (filterSelection) {
          selectedText = filterSelection(selectedText);
        }
        if (!toggleOff) {
          value = beforeRange + prefix + selectedText + suffix + afterRange;
          if (offsetStart) {
            pos += (prefix + suffix).length;
          } else {
            pos = from;
            offsetStart = prefix.length;
            offsetEnd = selectedText.length;
          }
        } else {
          value =
            beforeRange.substring(0, beforeRange.length - prefix.length) +
            selectedText +
            afterRange.substring(suffix.length);
          offsetStart = -selectedText.length - prefix.length;
          offsetEnd = selectedText.length;
        }
      }
      this.$emit("input", value);
      this.$nextTick(() => {
        $txt.focus();
        offsetStart = pos + (offsetStart || 0);
        offsetEnd = offsetStart + (offsetEnd || 0);
        $txt.setSelectionRange(offsetStart, offsetEnd);
      });
    },
    bold() {
      this.insert("**", "**", "bold");
    },
    italic() {
      this.insert("_", "_", "italics");
    },
    strikethrough() {
      this.insert("~~", "~~", "strikethrough");
    },
    link() {
      this.insert("[", "](http://)", "", { offsetStart: -8, offsetEnd: 7 });
    },
    quote() {
      this.insert("\n> ", "\n", "Blockquote", {});
    },
    image() {
      this.insert("![", "](http://)", "alt text", {
        offsetStart: -8,
        offsetEnd: 7
      });
    },
    code(e) {
      let sel = this.selection();
      if (sel && !e.shiftKey) {
        this.insert("`", "`", "code");
      } else {
        let lang = this.lang || "";
        let partialSel = sel.indexOf("\n") === -1;
        if (partialSel) {
          this.insert("\n```" + lang + "\n", "\n```\n", "// code");
        } else {
          this.insert("```" + lang + "\n", "```\n", "");
        }
      }
    },
    ol() {
      if (this.hasSelection()) {
        let {
          sel,
          selPos,
          beforeSel,
          afterSel,
          prevCRPos,
          beforeCR,
          afterCR
        } = this.selectionInfo();
        let partialSel = sel.indexOf("\n") === -1;
        if (!partialSel) {
          let indent = !sel.startsWith(" 1. ");
          if (indent) {
            let index = 1;
            this.insert("", "", " - ", {
              selectionAtEnd: true,
              filterSelection: v =>
                " 1. " +
                v.replace(/\n$/, "").replace(/\n/g, x => `\n ${++index}. `) +
                "\n"
            });
          } else {
            this.insert("", "", "", {
              filterValue: (v, opt) => {
                if (prevCRPos >= 0) {
                  let afterCRTrim = afterCR.replace(/^ - /, "");
                  beforeSel = beforeCR + afterCRTrim;
                  opt.pos -= afterCR.length - afterCRTrim.length;
                }
                return beforeSel + afterSel;
              },
              filterSelection: v =>
                v.replace(/^ 1. /g, "").replace(/\n \d+. /g, "\n")
            });
          }
        } else {
          this.insert("\n 1. ", "\n");
        }
      } else {
        this.insert("\n 1. ", "\n", "List Item", {
          offsetStart: -10,
          offsetEnd: 9
        });
      }
    },
    ul() {
      if (this.hasSelection()) {
        let {
          sel,
          selPos,
          beforeSel,
          afterSel,
          prevCRPos,
          beforeCR,
          afterCR
        } = this.selectionInfo();
        let partialSel = sel.indexOf("\n") === -1;
        if (!partialSel) {
          let indent = !sel.startsWith(" - ");
          if (indent) {
            this.insert("", "", " - ", {
              selectionAtEnd: true,
              filterSelection: v =>
                " - " + v.replace(/\n$/, "").replace(/\n/g, "\n - ") + "\n"
            });
          } else {
            this.insert("", "", "", {
              filterValue: (v, opt) => {
                if (prevCRPos >= 0) {
                  let afterCRTrim = afterCR.replace(/^ - /, "");
                  beforeSel = beforeCR + afterCRTrim;
                  opt.pos -= afterCR.length - afterCRTrim.length;
                }
                return beforeSel + afterSel;
              },
              filterSelection: v =>
                v.replace(/^ - /g, "").replace(/\n - /g, "\n")
            });
          }
        } else {
          this.insert("\n - ", "\n");
        }
      } else {
        this.insert("\n - ", "\n", "List Item", {
          offsetStart: -10,
          offsetEnd: 9
        });
      }
    },
    heading() {
      let sel = this.selection(),
        partialSel = sel.indexOf("\n") === -1;
      if (sel) {
        if (partialSel) {
          this.insert("\n## ", "\n", "");
        } else {
          this.insert("## ", "", "");
        }
      } else {
        this.insert("\n## ", "\n", "Heading", {
          offsetStart: -8,
          offsetEnd: 7
        });
      }
    },
    comment() {
      let {
        sel,
        selPos,
        beforeSel,
        afterSel,
        prevCRPos,
        beforeCR,
        afterCR
      } = this.selectionInfo();
      let comment = !sel.startsWith("//") && !afterCR.startsWith("//");
      if (comment) {
        if (!sel) {
          this.replace({
            value: beforeCR + "//" + afterCR + afterSel,
            selectionStart: selPos + "//".length
          });
        } else {
          this.insert("", "", "//", {
            selectionAtEnd: true,
            filterSelection: v =>
              "//" + v.replace(/\n$/, "").replace(/\n/g, "\n//") + "\n"
          });
        }
      } else {
        this.insert("", "", "", {
          filterValue: (v, opt) => {
            if (prevCRPos >= 0) {
              let afterCRTrim = afterCR.replace(/^\/\//, "");
              beforeSel = beforeCR + afterCRTrim;
              opt.pos -= afterCR.length - afterCRTrim.length;
            }
            return beforeSel + afterSel;
          },
          filterSelection: v => v.replace(/^\/\//g, "").replace(/\n\/\//g, "\n")
        });
      }
    },
    blockComment() {
      this.insert("/*\n", "*/\n", "");
    },
    undo() {
      if (history.length === 0) return false;
      let $txt = this.input();
      let lastState = history.pop();
      redo.push({
        value: $txt.value,
        selectionStart: $txt.selectionStart,
        selectionEnd: $txt.selectionEnd
      });
      this.replace(lastState);
      return true;
    },
    redo() {
      if (redo.length === 0) return false;
      let $txt = this.input();
      let lastState = redo.pop();
      history.push({
        value: $txt.value,
        selectionStart: $txt.selectionStart,
        selectionEnd: $txt.selectionEnd
      });
      this.replace(lastState);
      return true;
    },
    save() {
      this.$emit("save");
    },
    onkeydown(e) {
        if (e.key === "Escape" || e.keyCode === 27) {
          this.$emit('close');
          return;
        }
        let c = String.fromCharCode(e.keyCode).toLowerCase();
        if (c === '\t') { //tab: indent/unindent
          let indent = !e.shiftKey;
          if (indent) {
            this.insert('','','    ', {
              selectionAtEnd: true,
              filterSelection: v => "    " + v.replace(/\n$/,'').replace(/\n/g,"\n    ") + "\n"
            });
          } else {
            this.insert('','','', {
              filterValue:(v,opt) => {
                let { selPos, beforeSel, afterSel, prevCRPos, beforeCR, afterCR } = this.selectionInfo();
                if (prevCRPos >= 0) {
                  let afterCRTrim = afterCR.replace(/\t/g,'    ').replace(/^ ? ? ? ?/,'');
                  beforeSel = beforeCR + afterCRTrim;
                  opt.pos -= afterCR.length - afterCRTrim.length;
                }
                return beforeSel + afterSel;
              },
              filterSelection: v => v.replace(/\t/g,'    ').replace(/^ ? ? ? ?/g,'').replace(/\n    /g,"\n")
            });
          }
          e.preventDefault();
        }
        else if (e.ctrlKey)
        {
          if (c === 'z') { //z: undo/redo
            if (!e.shiftKey) {
              if (this.undo()) {
                e.preventDefault();
              }
            } else {
              if (this.redo()) {
                e.preventDefault();
              }
            }
          } else if (c === 'b' && !e.shiftKey) { //b: bold
            this.bold();
            e.preventDefault();
          } else if (c === 'h' && !e.shiftKey) { //h: heading
            this.heading();
            e.preventDefault();
          } else if (c === 'i' && !e.shiftKey) { //i: italic
            this.italic();
            e.preventDefault();
          } else if (c === 'q' && !e.shiftKey) { //q: blockquote
            this.quote();
            e.preventDefault();
          } else if (c === 'l') { //l: link/image
            if (!e.shiftKey) {
              this.link();
              e.preventDefault();
            } else {
              this.image();
              e.preventDefault();
            }
          } else if ((c === 'k' || c === ',' || e.key === '<' || e.key === '>' || e.keyCode === 188)) { //<>: code
            this.code(e);
            e.preventDefault();
          } else if (c === 's' && !e.shiftKey) { //s: save
            this.save();
            e.preventDefault();
          } else if (c === '/' || e.key === '/') {
            this.comment();
            e.preventDefault();
          } else if ((c === '?' || e.key === '?') && e.shiftKey) {
            this.blockComment();
            e.preventDefault();
          }
        }
        else if (e.altKey) {
          if (e.key === '1' || e.key === '0') {
            this.ol();
            e.preventDefault();
          } else if (e.key === '-') {
            this.ul();
            e.preventDefault();
          } else if (e.key === 's') {
            this.strikethrough();
            e.preventDefault();
          }
        }
      },
      ensureMarkdownBlock() {
        if (this.target.value.indexOf("{{#markdown") == -1) {
            let prefix = "{{#markdown}}\n";
            var selection = {
              start: this.target.selectionStart,
              end: this.target.selectionEnd
            };
            this.target.value = prefix + this.target.value + "\n{{/markdown}}";
            this.target.setSelectionRange(
              selection.start + prefix.length,
              selection.end + prefix.length
            );
          }  
      }
  };

  let ACTIVE_KEYS = '\t,b,n,h,i,q,l,k,<,>,/,?,1,0'.split(',');
  ops.target.addEventListener('keydown', function(e){
    var isActiveKey = ACTIVE_KEYS.indexOf(e.key) >= 0;
    if (isActiveKey && (e.ctrlKey || e.altKey)) {
        ops.ensureMarkdownBlock();
    }
    ops.onkeydown(e);
  });

  let btns = $editor.querySelectorAll(".editor-toolbar [data-cmd]");
  for (let i = 0; i < btns.length; i++) {
    let el = btns[i];
    let cmd = el.getAttribute("data-cmd");
    el.addEventListener("click", function(e) {
      if (ops[cmd]) {
        if (['save','undo','redo'].indexOf(cmd) === -1) {
          ops.ensureMarkdownBlock();
        }
        ops[cmd](e);
      }
    });
  }
}
