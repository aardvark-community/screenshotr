export function SetFocusToElement(id) {
    //console.log("SetFocusToElement(" + id + ") before")
    document.getElementById(id)?.focus();
    //console.log("SetFocusToElement(" + id + ") after")
}

export function GetInputText(element) {
    //console.log("[GetInputText] begin");
    //console.log(element);
    //console.log(element.value);
    //console.log("[GetInputText] end");
    return element.value;
}

export function GetBoundingClientRect(element) {
    if (element === null) return null;
    return element.getBoundingClientRect();
}

