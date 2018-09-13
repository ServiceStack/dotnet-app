$(".live-template").each(function(){

    var el = $(this)
    el.find("textarea").on("input", function(){

        var request = { template: el.find("textarea").val() }

        $.ajax({
            type: "POST",
            url: "/template/eval" + location.search,
            data: JSON.stringify(request),
            contentType: "application/json",
            dataType: "html"
        }).done(function(data){
            el.removeClass('error').find(".output").html(data)
        }).fail(function(jqxhr){ handleError(el, jqxhr) })
    })
    .trigger("input")

})

function handleError(el, jqxhr) {
    try {
        console.log('template error:', jqxhr.status, jqxhr.statusText)
        el.addClass('error')
        var errorResponse = JSON.parse(jqxhr.responseText);
        var status = errorResponse.responseStatus;
        if (status) {
            el.find('.output').html('<div class="alert alert-danger"><pre>' + status.errorCode + ' ' + status.message +
             '\n\nStackTrace:\n' + status.stackTrace + '</pre></div>')
        }
    } catch(e) {
        el.find('.output').html('<div class="alert alert-danger"><pre>' + jqxhr.status + ' ' + jqxhr.statusText + '</pre></div>')
    }
}

function queryStringParams(qs) {
    qs = (qs || document.location.search).split('+').join(' ')
    var params = {}, tokens, re = /[?&]?([^=]+)=([^&]*)/g
    while (tokens = re.exec(qs)) {
        params[tokens[1]] = tokens[2];
    }
    return params;
}

$.fn.ajaxSubmit = function(opt) {
    return this.each(function(){ 
        $(this).submit(function(e){ 
            e.preventDefault();

            var f = $(this);
            f.removeClass('is-invalid').find('.invalid-feedback').html('');

            var data = {};
            f.find("input").removeClass('is-invalid').each(function(){ data[this.name] = this.value });
            $.ajax({ 
                url: f.attr('action'),
                method: "POST",
                data: JSON.stringify(data),
                contentType: 'application/json',
                dataType: opt.dataType || 'json',
                success: opt.success,
                error: opt.error || function(jq,status,errMsg) { ajaxSubmitError(f, jq) }
            })

        }) 
    });
}

function ajaxSubmitError(f, jqxhr) {
    try {
        var errorResponse = JSON.parse(jqxhr.responseText);
        var status = errorResponse.responseStatus;
        if (status) {
            if (status.errors && status.errors.length > 0) {
                for (var i=0; i<status.errors.length; i++) {
                    var fieldError = status.errors[i];
                    f.find("[name=" + fieldError.fieldName + "]").addClass('is-invalid').next('.invalid-feedback').html(fieldError.message);
                }
            }
            else {
                f.find('.error-summary').html('<div class="alert alert-danger">' + status.message + '</div>')
            }
            return;
        }
    } catch(e) {}
    f.find('.error-summary').html('<div class="alert alert-danger"><pre>' + jqxhr.status + ' ' + jqxhr.statusText + '</pre></div>')
}
