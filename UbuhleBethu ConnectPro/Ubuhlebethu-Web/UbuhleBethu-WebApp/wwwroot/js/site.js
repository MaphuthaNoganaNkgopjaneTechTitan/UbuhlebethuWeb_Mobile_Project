// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function() {
    var forms = document.querySelectorAll("form");
    forms.forEach(function(form) {
        form.addEventListener("submit", function(e) {
            var submitBtn = form.querySelector('button[type="submit"], input[type="submit"]');
            if (submitBtn) {
                if (submitBtn.tagName.toLowerCase() === 'button') {
                    if (!submitBtn.disabled) {
                        submitBtn.disabled = true;
                        if (!submitBtn.querySelector('.spinner-border')) {
                            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true" style="margin-right: 5px;"></span>' + submitBtn.innerHTML;
                        }
                    }
                } else if (submitBtn.tagName.toLowerCase() === 'input') {
                    if (!submitBtn.disabled) {
                        submitBtn.disabled = true;
                        submitBtn.value = "Processing...";
                    }
                }
            }
        });
    });

    var inputs = document.querySelectorAll("input, select, textarea");
    inputs.forEach(function(input) {
        input.addEventListener("invalid", function() {
            input.setAttribute("aria-invalid", "true");
        });
        input.addEventListener("input", function() {
            if (input.validity.valid) {
                input.removeAttribute("aria-invalid");
            }
        });
    });
});
