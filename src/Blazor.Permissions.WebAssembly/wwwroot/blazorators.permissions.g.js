// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

console.groupCollapsed(
    '%cblazorators%c permissions %cJavaScript loaded',
    'background: purple; color: white; padding: 1px 3px; border-radius: 3px;',
    'color: cyan;', 'color: initial;');

const query = async (descriptor) => {
    // The generated C# layer hands us a PermissionDescriptor with a
    // `Name` property (`JsonPropertyNameAttribute("name")` -> "name").
    // Blazor's default serializer round-trips both spellings, so
    // forward whichever made it across as the canonical TS
    // `PermissionDescriptor`.
    const status = await navigator.permissions.query({
        name: descriptor?.name ?? descriptor?.Name
    });

    // Reshape to match the generated `PermissionStatus` DTO. The
    // generator strips `extends EventTarget` and the `onchange` event
    // hook from the C# DTO, so only `name` and `state` survive --
    // expose just those keys to keep the wire payload tight.
    return {
        Name: status.name,
        State: status.state
    };
};

console.log('%O %cfunction %cdefined ✅.', query, 'color: magenta;', 'color: initial;');

window.blazorators = Object.assign({}, window.blazorators, {
    permissions: {
        query
    }
});

console.groupEnd();
