// Save content to local storage to preserve state across page reloads
let forms = document.querySelectorAll('form[data-save-drafts]');
for (let i = 0; i < forms.length; i++) {
    let form = forms[i];
    let monitorInputs = form.querySelectorAll('input[type=text],textarea');
    let keys = [];
    for (let j=0; j < monitorInputs.length; j++) {
        let input = monitorInputs[j];
        let key = 'drafts.' + location.pathname + '.' + (input.getAttribute('name') || input.id);
        keys.push(key);
        let draftValue = localStorage.getItem(key);
        if (draftValue) {
            input.value = draftValue;
        }
        input.addEventListener('input', function(e){
            localStorage.setItem(key, this.value);
        }, false);
    }
    form.addEventListener('submit', function(e) {
        keys.forEach(key => localStorage.removeItem(key));
    });
}

// Enable autogrowing textareas
let textAreas = document.querySelectorAll("textarea[data-autogrow]");
for (let i = 0; i < textAreas.length; i++) {
  textAreas[i].addEventListener("input", autogrow);
  autogrow({ target: textAreas[i] });
}

function autogrow(e) {
  let el = e.target;
  let minHeignt = 150;
  el.style.height = "5px";
  el.style.height = Math.max(el.scrollHeight, minHeignt) + "px";
}

// Enable Live Preview of new Content
textAreas = document.querySelectorAll("textarea[data-livepreview]");
for (let i = 0; i < textAreas.length; i++) {
  textAreas[i].addEventListener("input", livepreview, false);
  livepreview({ target: textAreas[i] });
}

function livepreview(e) {
  let el = e.target;
  let sel = el.getAttribute("data-livepreview");

  if (el.value.trim() == "") {
    document.querySelector(sel).innerHTML = "<div style='text-align:center;padding-top:20px;color:#999'>Live Preview</div>";
    return;
  }

  let formData = new FormData();
  formData.append("content", el.value);

  fetch("/preview", {
    method: "post",
    body: formData
  }).then(function(r) { return r.text(); })
    .then(function(r) { document.querySelector(sel).innerHTML = r; });
}

// Ctrl + Click on page to edit
let posts = document.querySelectorAll("[data-edit-path]");
for (let i = 0; i < posts.length; i++) {
  let el = posts[i];
  let url = el.getAttribute("data-edit-path");
  el.addEventListener("click", function(e) {
      if (e.ctrlKey) {
          location.href = url;
      }
  });
}

// Auto Link Headings with id attributes
var headings = document.querySelectorAll(".post-content h2[id],.post-content h3[id],.post-content h4[id],.post-content h5[id]");
for (let i = 0; i < headings.length; i++) {
  let el = headings[i];
  el.addEventListener("click", function(e) {
      location.href = "#" + this.id;
  });
}
