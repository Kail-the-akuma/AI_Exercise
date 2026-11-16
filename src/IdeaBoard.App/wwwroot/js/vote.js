/*
    Progressive enhancement for the vote button, and nothing else.

    Without this file the button is an ordinary form: it posts, the server answers with a
    redirect, and the page reloads at the same place. With it, the same form is posted in
    the background and only the button and the count change. The two paths run through the
    same handler on the server, so the behaviour is identical either way.

    This script never works out a number. Everything it writes on screen arrives already
    worded in the body of the server's answer, which is why there is not a single
    arithmetic operation here and not a single piece of interface wording either. A count
    the server did not produce cannot reach the screen.

    The same holds for the two states the stylesheet reads. They arrive as answers and not
    as the count they were decided from, so this file never compares a number to a
    threshold and never reads a state back out of the text it has just written.

    Served from /_content/IdeaBoard.App/js/vote.js, because it lives in a Razor class
    library and not in the host.
*/

(function ()
{
    "use strict";

    var formSelector = "[data-vote-form]";
    var buttonSelector = "[data-vote-button]";
    var countSelector = "[data-vote-count]";
    var busyFlag = "voteBusy";

    /*
        The two state classes the stylesheet needs, named here the same way the selectors
        above are. They are markup the partial also writes, so this file does not decide
        when they apply: the server says so in its answer, and the names are the only part
        that has to be spelled the same in both places. A scenario presses the button with
        this script running, reloads, and compares the two sets, which is what keeps the
        spelling honest.
    */
    var noVotesClass = "sem-votos";
    var votedClass = "votado";

    // Nothing is intercepted on a browser without fetch. The form then posts normally,
    // which is the behaviour the application is built on anyway.
    if (typeof window.fetch !== "function")
    {
        return;
    }

    /*
        Writes the server's answer on screen. Each value is taken as it arrived: the
        wording of the button, the wording of the count, the two states the stylesheet
        reads, and the handler the form should ask for next.

        The states matter because the wording alone does not carry them. Before this, a
        button could say "Votado" without the mark that distinguishes it, and a count that
        had just left zero could stay in the quiet treatment reserved for an idea nobody
        has supported. Nothing errored and nothing looked broken, which is why a reload
        was the only thing that put it right.
    */
    function applyResponse(form, response)
    {
        var button = form.querySelector(buttonSelector);
        var count = form.querySelector(countSelector);

        applyStateClass(form, noVotesClass, response.hasNoVotes);

        if (button !== null)
        {
            button.textContent = response.buttonText;
            applyStateClass(button, votedClass, response.hasVoted);
        }

        if (count !== null)
        {
            count.textContent = response.voteCountText;
        }

        setHandler(form, response.nextHandler);
    }


    /*
        Puts a state class on or takes it off, according to what the server answered. The
        flag is read as the server sent it and is not worked out from anything on screen,
        which is the same rule the wording follows.
    */
    function applyStateClass(element, className, wanted)
    {
        if (wanted === true)
        {
            element.classList.add(className);
            return;
        }

        element.classList.remove(className);
    }

    /*
        Points the form at the handler the server named. The handler is a query string
        value on the action the page rendered, so that the form stays a plain form: if the
        person reloads or the script stops working from here on, the next submission still
        asks for the same thing.
    */
    function setHandler(form, handler)
    {
        var action = new URL(form.action, window.location.href);

        action.searchParams.set("handler", handler);
        form.setAttribute("action", action.toString());
    }

    /*
        Posts the form the way the browser would, with the antiforgery token the server
        put inside it. The Accept header is what makes the server answer with the state as
        JSON instead of a redirect.
    */
    function sendVote(form)
    {
        return fetch(form.action, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Accept": "application/json" },
            body: new FormData(form)
        });
    }

    /*
        Falls back to the ordinary submission. Calling submit on the element does not
        raise the submit event, so this cannot come back round to the handler below.
    */
    function fallBackToNormalPost(form)
    {
        form.submit();
    }

    function onSubmit(event)
    {
        var form = event.target;

        if (!(form instanceof HTMLFormElement))
        {
            return;
        }

        if (!form.matches(formSelector))
        {
            return;
        }

        // An answer already on its way owns the button and the count. Letting a second
        // one race it would risk writing the older of the two states on screen.
        if (form.dataset[busyFlag] === "true")
        {
            event.preventDefault();
            return;
        }

        event.preventDefault();
        form.dataset[busyFlag] = "true";

        sendVote(form).then(function (response)
        {
            if (!response.ok)
            {
                delete form.dataset[busyFlag];
                fallBackToNormalPost(form);
                return;
            }

            return response.json().then(function (state)
            {
                applyResponse(form, state);
                delete form.dataset[busyFlag];
            });
        }).catch(function ()
        {
            delete form.dataset[busyFlag];
            fallBackToNormalPost(form);
        });
    }

    document.addEventListener("submit", onSubmit);
}());
