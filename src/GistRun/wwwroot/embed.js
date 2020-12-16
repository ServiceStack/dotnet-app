document.querySelectorAll('.gistrun[data-gist]').forEach(function(e) {
let height = e.getAttribute('data-height');
let iframe = document.createElement('iframe'),s=iframe.style;
s.width='100%';
s.height=height+'px';
s.border='1px solid #ddd';
let sb='';
e.getAttributeNames().forEach(function(k) {
  if (k.indexOf('data-')===0) { 
      sb += sb ? '&' : '?'; 
      sb += k.substring(5) + '=' + encodeURIComponent(e.getAttribute(k));
  }
});
iframe.src='/embed'+sb;
e.replaceWith(iframe);
});