// Light/dark toggle. The inline script in App.razor <head> applies a saved choice before first paint;
// this file handles clicks and keeps the button label right, including after Blazor's enhanced navigation.
(function () {
    var KEY = "theme";
    var root = document.documentElement;

    function saved() {
        try {
            var t = localStorage.getItem(KEY);
            return t === "light" || t === "dark" ? t : null;
        } catch (e) {
            return null;
        }
    }

    function current() {
        return root.dataset.theme || (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
    }

    function sync() {
        var s = saved();
        if (s) root.dataset.theme = s;
        var next = current() === "dark" ? "light" : "dark";
        document.querySelectorAll(".theme-toggle").forEach(function (b) {
            b.setAttribute("aria-label", "Switch to " + next + " mode");
            b.textContent = next === "dark" ? "Dark mode" : "Light mode";
        });
    }

    document.addEventListener("click", function (e) {
        if (!e.target.closest || !e.target.closest(".theme-toggle")) return;
        var next = current() === "dark" ? "light" : "dark";
        root.dataset.theme = next;
        try { localStorage.setItem(KEY, next); } catch (err) { }
        sync();
    });

    document.addEventListener("DOMContentLoaded", sync);
    if (document.readyState !== "loading") sync();
    if (window.Blazor) window.Blazor.addEventListener("enhancedload", sync);
    else document.addEventListener("enhancedload", sync);
})();
