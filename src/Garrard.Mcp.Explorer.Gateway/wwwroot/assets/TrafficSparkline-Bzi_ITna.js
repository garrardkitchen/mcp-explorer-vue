import{$t as e,Gt as t,In as n,Kt as r,Nn as i,Qt as a,Ut as o,Vt as s,Wt as c,c as l,cn as u,dn as d,fn as f,ht as p,jt as m,mn as h,pn as g,qt as _,rn as v,s as y,t as b,u as x,un as ee,vn as S,y as C,yn as te}from"./_plugin-vue_export-helper-DLCbmiWh.js";var w={name:`Card`,extends:{name:`BaseCard`,extends:x,style:C.extend({name:`card`,style:`
    .p-card {
        background: dt('card.background');
        color: dt('card.color');
        box-shadow: dt('card.shadow');
        border-radius: dt('card.border.radius');
        display: flex;
        flex-direction: column;
    }

    .p-card-caption {
        display: flex;
        flex-direction: column;
        gap: dt('card.caption.gap');
    }

    .p-card-body {
        padding: dt('card.body.padding');
        display: flex;
        flex-direction: column;
        gap: dt('card.body.gap');
    }

    .p-card-title {
        font-size: dt('card.title.font.size');
        font-weight: dt('card.title.font.weight');
    }

    .p-card-subtitle {
        color: dt('card.subtitle.color');
    }
`,classes:{root:`p-card p-component`,header:`p-card-header`,body:`p-card-body`,caption:`p-card-caption`,title:`p-card-title`,subtitle:`p-card-subtitle`,content:`p-card-content`,footer:`p-card-footer`}}),provide:function(){return{$pcCard:this,$parentInstance:this}}},inheritAttrs:!1};function T(e,t,n,i,a,o){return u(),_(`div`,v({class:e.cx(`root`)},e.ptmi(`root`)),[e.$slots.header?(u(),_(`div`,v({key:0,class:e.cx(`header`)},e.ptm(`header`)),[d(e.$slots,`header`)],16)):r(``,!0),c(`div`,v({class:e.cx(`body`)},e.ptm(`body`)),[e.$slots.title||e.$slots.subtitle?(u(),_(`div`,v({key:0,class:e.cx(`caption`)},e.ptm(`caption`)),[e.$slots.title?(u(),_(`div`,v({key:0,class:e.cx(`title`)},e.ptm(`title`)),[d(e.$slots,`title`)],16)):r(``,!0),e.$slots.subtitle?(u(),_(`div`,v({key:1,class:e.cx(`subtitle`)},e.ptm(`subtitle`)),[d(e.$slots,`subtitle`)],16)):r(``,!0)],16)):r(``,!0),c(`div`,v({class:e.cx(`content`)},e.ptm(`content`)),[d(e.$slots,`content`)],16),e.$slots.footer?(u(),_(`div`,v({key:1,class:e.cx(`footer`)},e.ptm(`footer`)),[d(e.$slots,`footer`)],16)):r(``,!0)],16)],16)}w.render=T;var E={name:`InputGroup`,extends:{name:`BaseInputGroup`,extends:x,style:C.extend({name:`inputgroup`,style:`
    .p-inputgroup,
    .p-inputgroup .p-iconfield,
    .p-inputgroup .p-floatlabel,
    .p-inputgroup .p-iftalabel {
        display: flex;
        align-items: stretch;
        width: 100%;
    }

    .p-inputgroup .p-floatlabel .p-inputwrapper,
    .p-inputgroup .p-iftalabel .p-inputwrapper {
        display: inline-flex;
    }

    .p-inputgroup .p-inputtext,
    .p-inputgroup .p-inputwrapper {
        flex: 1 1 auto;
        width: 1%;
    }

    .p-inputgroupaddon {
        display: flex;
        align-items: center;
        justify-content: center;
        padding: dt('inputgroup.addon.padding');
        background: dt('inputgroup.addon.background');
        color: dt('inputgroup.addon.color');
        border-block-start: 1px solid dt('inputgroup.addon.border.color');
        border-block-end: 1px solid dt('inputgroup.addon.border.color');
        min-width: dt('inputgroup.addon.min.width');
    }

    .p-inputgroupaddon:first-child,
    .p-inputgroupaddon + .p-inputgroupaddon {
        border-inline-start: 1px solid dt('inputgroup.addon.border.color');
    }

    .p-inputgroupaddon:last-child {
        border-inline-end: 1px solid dt('inputgroup.addon.border.color');
    }

    .p-inputgroupaddon:has(.p-button) {
        padding: 0;
        overflow: hidden;
    }

    .p-inputgroupaddon .p-button {
        border-radius: 0;
    }

    .p-inputgroup > .p-component,
    .p-inputgroup > .p-inputwrapper > .p-component,
    .p-inputgroup > .p-iconfield > .p-component,
    .p-inputgroup > .p-floatlabel > .p-component,
    .p-inputgroup > .p-floatlabel > .p-inputwrapper > .p-component,
    .p-inputgroup > .p-iftalabel > .p-component,
    .p-inputgroup > .p-iftalabel > .p-inputwrapper > .p-component {
        border-radius: 0;
        margin: 0;
    }

    .p-inputgroupaddon:first-child,
    .p-inputgroup > .p-component:first-child,
    .p-inputgroup > .p-inputwrapper:first-child > .p-component,
    .p-inputgroup > .p-iconfield:first-child > .p-component,
    .p-inputgroup > .p-floatlabel:first-child > .p-component,
    .p-inputgroup > .p-floatlabel:first-child > .p-inputwrapper > .p-component,
    .p-inputgroup > .p-iftalabel:first-child > .p-component,
    .p-inputgroup > .p-iftalabel:first-child > .p-inputwrapper > .p-component {
        border-start-start-radius: dt('inputgroup.addon.border.radius');
        border-end-start-radius: dt('inputgroup.addon.border.radius');
    }

    .p-inputgroupaddon:last-child,
    .p-inputgroup > .p-component:last-child,
    .p-inputgroup > .p-inputwrapper:last-child > .p-component,
    .p-inputgroup > .p-iconfield:last-child > .p-component,
    .p-inputgroup > .p-floatlabel:last-child > .p-component,
    .p-inputgroup > .p-floatlabel:last-child > .p-inputwrapper > .p-component,
    .p-inputgroup > .p-iftalabel:last-child > .p-component,
    .p-inputgroup > .p-iftalabel:last-child > .p-inputwrapper > .p-component {
        border-start-end-radius: dt('inputgroup.addon.border.radius');
        border-end-end-radius: dt('inputgroup.addon.border.radius');
    }

    .p-inputgroup .p-component:focus,
    .p-inputgroup .p-component.p-focus,
    .p-inputgroup .p-inputwrapper-focus,
    .p-inputgroup .p-component:focus ~ label,
    .p-inputgroup .p-component.p-focus ~ label,
    .p-inputgroup .p-inputwrapper-focus ~ label,
    .p-inputgroup .p-floatlabel .p-inputwrapper ~ label,
    .p-inputgroup .p-iftalabel .p-inputwrapper ~ label {
        z-index: 1;
    }

    .p-inputgroup > .p-button:not(.p-button-icon-only) {
        width: auto;
    }

    .p-inputgroup .p-iconfield + .p-iconfield .p-inputtext {
        border-inline-start: 0;
    }
`,classes:{root:`p-inputgroup`}}),provide:function(){return{$pcInputGroup:this,$parentInstance:this}}},inheritAttrs:!1};function D(e,t,n,r,i,a){return u(),_(`div`,v({class:e.cx(`root`)},e.ptmi(`root`)),[d(e.$slots,`default`)],16)}E.render=D;var O={name:`InputGroupAddon`,extends:{name:`BaseInputGroupAddon`,extends:x,style:C.extend({name:`inputgroupaddon`,classes:{root:`p-inputgroupaddon`}}),provide:function(){return{$pcInputGroupAddon:this,$parentInstance:this}}},inheritAttrs:!1};function k(e,t,n,r,i,a){return u(),_(`div`,v({class:e.cx(`root`)},e.ptmi(`root`)),[d(e.$slots,`default`)],16)}O.render=k;var ne=C.extend({name:`message`,style:`
    .p-message {
        display: grid;
        grid-template-rows: 1fr;
        border-radius: dt('message.border.radius');
        outline-width: dt('message.border.width');
        outline-style: solid;
    }

    .p-message-content-wrapper {
        min-height: 0;
    }

    .p-message-content {
        display: flex;
        align-items: center;
        padding: dt('message.content.padding');
        gap: dt('message.content.gap');
    }

    .p-message-icon {
        flex-shrink: 0;
    }

    .p-message-close-button {
        display: flex;
        align-items: center;
        justify-content: center;
        flex-shrink: 0;
        margin-inline-start: auto;
        overflow: hidden;
        position: relative;
        width: dt('message.close.button.width');
        height: dt('message.close.button.height');
        border-radius: dt('message.close.button.border.radius');
        background: transparent;
        transition:
            background dt('message.transition.duration'),
            color dt('message.transition.duration'),
            outline-color dt('message.transition.duration'),
            box-shadow dt('message.transition.duration'),
            opacity 0.3s;
        outline-color: transparent;
        color: inherit;
        padding: 0;
        border: none;
        cursor: pointer;
        user-select: none;
    }

    .p-message-close-icon {
        font-size: dt('message.close.icon.size');
        width: dt('message.close.icon.size');
        height: dt('message.close.icon.size');
    }

    .p-message-close-button:focus-visible {
        outline-width: dt('message.close.button.focus.ring.width');
        outline-style: dt('message.close.button.focus.ring.style');
        outline-offset: dt('message.close.button.focus.ring.offset');
    }

    .p-message-info {
        background: dt('message.info.background');
        outline-color: dt('message.info.border.color');
        color: dt('message.info.color');
        box-shadow: dt('message.info.shadow');
    }

    .p-message-info .p-message-close-button:focus-visible {
        outline-color: dt('message.info.close.button.focus.ring.color');
        box-shadow: dt('message.info.close.button.focus.ring.shadow');
    }

    .p-message-info .p-message-close-button:hover {
        background: dt('message.info.close.button.hover.background');
    }

    .p-message-info.p-message-outlined {
        color: dt('message.info.outlined.color');
        outline-color: dt('message.info.outlined.border.color');
    }

    .p-message-info.p-message-simple {
        color: dt('message.info.simple.color');
    }

    .p-message-success {
        background: dt('message.success.background');
        outline-color: dt('message.success.border.color');
        color: dt('message.success.color');
        box-shadow: dt('message.success.shadow');
    }

    .p-message-success .p-message-close-button:focus-visible {
        outline-color: dt('message.success.close.button.focus.ring.color');
        box-shadow: dt('message.success.close.button.focus.ring.shadow');
    }

    .p-message-success .p-message-close-button:hover {
        background: dt('message.success.close.button.hover.background');
    }

    .p-message-success.p-message-outlined {
        color: dt('message.success.outlined.color');
        outline-color: dt('message.success.outlined.border.color');
    }

    .p-message-success.p-message-simple {
        color: dt('message.success.simple.color');
    }

    .p-message-warn {
        background: dt('message.warn.background');
        outline-color: dt('message.warn.border.color');
        color: dt('message.warn.color');
        box-shadow: dt('message.warn.shadow');
    }

    .p-message-warn .p-message-close-button:focus-visible {
        outline-color: dt('message.warn.close.button.focus.ring.color');
        box-shadow: dt('message.warn.close.button.focus.ring.shadow');
    }

    .p-message-warn .p-message-close-button:hover {
        background: dt('message.warn.close.button.hover.background');
    }

    .p-message-warn.p-message-outlined {
        color: dt('message.warn.outlined.color');
        outline-color: dt('message.warn.outlined.border.color');
    }

    .p-message-warn.p-message-simple {
        color: dt('message.warn.simple.color');
    }

    .p-message-error {
        background: dt('message.error.background');
        outline-color: dt('message.error.border.color');
        color: dt('message.error.color');
        box-shadow: dt('message.error.shadow');
    }

    .p-message-error .p-message-close-button:focus-visible {
        outline-color: dt('message.error.close.button.focus.ring.color');
        box-shadow: dt('message.error.close.button.focus.ring.shadow');
    }

    .p-message-error .p-message-close-button:hover {
        background: dt('message.error.close.button.hover.background');
    }

    .p-message-error.p-message-outlined {
        color: dt('message.error.outlined.color');
        outline-color: dt('message.error.outlined.border.color');
    }

    .p-message-error.p-message-simple {
        color: dt('message.error.simple.color');
    }

    .p-message-secondary {
        background: dt('message.secondary.background');
        outline-color: dt('message.secondary.border.color');
        color: dt('message.secondary.color');
        box-shadow: dt('message.secondary.shadow');
    }

    .p-message-secondary .p-message-close-button:focus-visible {
        outline-color: dt('message.secondary.close.button.focus.ring.color');
        box-shadow: dt('message.secondary.close.button.focus.ring.shadow');
    }

    .p-message-secondary .p-message-close-button:hover {
        background: dt('message.secondary.close.button.hover.background');
    }

    .p-message-secondary.p-message-outlined {
        color: dt('message.secondary.outlined.color');
        outline-color: dt('message.secondary.outlined.border.color');
    }

    .p-message-secondary.p-message-simple {
        color: dt('message.secondary.simple.color');
    }

    .p-message-contrast {
        background: dt('message.contrast.background');
        outline-color: dt('message.contrast.border.color');
        color: dt('message.contrast.color');
        box-shadow: dt('message.contrast.shadow');
    }

    .p-message-contrast .p-message-close-button:focus-visible {
        outline-color: dt('message.contrast.close.button.focus.ring.color');
        box-shadow: dt('message.contrast.close.button.focus.ring.shadow');
    }

    .p-message-contrast .p-message-close-button:hover {
        background: dt('message.contrast.close.button.hover.background');
    }

    .p-message-contrast.p-message-outlined {
        color: dt('message.contrast.outlined.color');
        outline-color: dt('message.contrast.outlined.border.color');
    }

    .p-message-contrast.p-message-simple {
        color: dt('message.contrast.simple.color');
    }

    .p-message-text {
        font-size: dt('message.text.font.size');
        font-weight: dt('message.text.font.weight');
    }

    .p-message-icon {
        font-size: dt('message.icon.size');
        width: dt('message.icon.size');
        height: dt('message.icon.size');
    }

    .p-message-sm .p-message-content {
        padding: dt('message.content.sm.padding');
    }

    .p-message-sm .p-message-text {
        font-size: dt('message.text.sm.font.size');
    }

    .p-message-sm .p-message-icon {
        font-size: dt('message.icon.sm.size');
        width: dt('message.icon.sm.size');
        height: dt('message.icon.sm.size');
    }

    .p-message-sm .p-message-close-icon {
        font-size: dt('message.close.icon.sm.size');
        width: dt('message.close.icon.sm.size');
        height: dt('message.close.icon.sm.size');
    }

    .p-message-lg .p-message-content {
        padding: dt('message.content.lg.padding');
    }

    .p-message-lg .p-message-text {
        font-size: dt('message.text.lg.font.size');
    }

    .p-message-lg .p-message-icon {
        font-size: dt('message.icon.lg.size');
        width: dt('message.icon.lg.size');
        height: dt('message.icon.lg.size');
    }

    .p-message-lg .p-message-close-icon {
        font-size: dt('message.close.icon.lg.size');
        width: dt('message.close.icon.lg.size');
        height: dt('message.close.icon.lg.size');
    }

    .p-message-outlined {
        background: transparent;
        outline-width: dt('message.outlined.border.width');
    }

    .p-message-simple {
        background: transparent;
        outline-color: transparent;
        box-shadow: none;
    }

    .p-message-simple .p-message-content {
        padding: dt('message.simple.content.padding');
    }

    .p-message-outlined .p-message-close-button:hover,
    .p-message-simple .p-message-close-button:hover {
        background: transparent;
    }

    .p-message-enter-active {
        animation: p-animate-message-enter 0.3s ease-out forwards;
        overflow: hidden;
    }

    .p-message-leave-active {
        animation: p-animate-message-leave 0.15s ease-in forwards;
        overflow: hidden;
    }

    @keyframes p-animate-message-enter {
        from {
            opacity: 0;
            grid-template-rows: 0fr;
        }
        to {
            opacity: 1;
            grid-template-rows: 1fr;
        }
    }

    @keyframes p-animate-message-leave {
        from {
            opacity: 1;
            grid-template-rows: 1fr;
        }
        to {
            opacity: 0;
            margin: 0;
            grid-template-rows: 0fr;
        }
    }
`,classes:{root:function(e){var t=e.props;return[`p-message p-component p-message-`+t.severity,{"p-message-outlined":t.variant===`outlined`,"p-message-simple":t.variant===`simple`,"p-message-sm":t.size===`small`,"p-message-lg":t.size===`large`}]},contentWrapper:`p-message-content-wrapper`,content:`p-message-content`,icon:`p-message-icon`,text:`p-message-text`,closeButton:`p-message-close-button`,closeIcon:`p-message-close-icon`}}),re={name:`BaseMessage`,extends:x,props:{severity:{type:String,default:`info`},closable:{type:Boolean,default:!1},life:{type:Number,default:null},icon:{type:String,default:void 0},closeIcon:{type:String,default:void 0},closeButtonProps:{type:null,default:null},size:{type:String,default:null},variant:{type:String,default:null}},style:ne,provide:function(){return{$pcMessage:this,$parentInstance:this}}};function A(e){"@babel/helpers - typeof";return A=typeof Symbol==`function`&&typeof Symbol.iterator==`symbol`?function(e){return typeof e}:function(e){return e&&typeof Symbol==`function`&&e.constructor===Symbol&&e!==Symbol.prototype?`symbol`:typeof e},A(e)}function j(e,t,n){return(t=ie(t))in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function ie(e){var t=M(e,`string`);return A(t)==`symbol`?t:t+``}function M(e,t){if(A(e)!=`object`||!e)return e;var n=e[Symbol.toPrimitive];if(n!==void 0){var r=n.call(e,t);if(A(r)!=`object`)return r;throw TypeError(`@@toPrimitive must return a primitive value.`)}return(t===`string`?String:Number)(e)}var N={name:`Message`,extends:re,inheritAttrs:!1,emits:[`close`,`life-end`],timeout:null,data:function(){return{visible:!0}},mounted:function(){var e=this;this.life&&setTimeout(function(){e.visible=!1,e.$emit(`life-end`)},this.life)},methods:{close:function(e){this.visible=!1,this.$emit(`close`,e)}},computed:{closeAriaLabel:function(){return this.$primevue.config.locale.aria?this.$primevue.config.locale.aria.close:void 0},dataP:function(){return p(j(j({outlined:this.variant===`outlined`,simple:this.variant===`simple`},this.severity,this.severity),this.size,this.size))}},directives:{ripple:y},components:{TimesIcon:l}};function P(e){"@babel/helpers - typeof";return P=typeof Symbol==`function`&&typeof Symbol.iterator==`symbol`?function(e){return typeof e}:function(e){return e&&typeof Symbol==`function`&&e.constructor===Symbol&&e!==Symbol.prototype?`symbol`:typeof e},P(e)}function F(e,t){var n=Object.keys(e);if(Object.getOwnPropertySymbols){var r=Object.getOwnPropertySymbols(e);t&&(r=r.filter(function(t){return Object.getOwnPropertyDescriptor(e,t).enumerable})),n.push.apply(n,r)}return n}function I(e){for(var t=1;t<arguments.length;t++){var n=arguments[t]==null?{}:arguments[t];t%2?F(Object(n),!0).forEach(function(t){L(e,t,n[t])}):Object.getOwnPropertyDescriptors?Object.defineProperties(e,Object.getOwnPropertyDescriptors(n)):F(Object(n)).forEach(function(t){Object.defineProperty(e,t,Object.getOwnPropertyDescriptor(n,t))})}return e}function L(e,t,n){return(t=R(t))in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function R(e){var t=z(e,`string`);return P(t)==`symbol`?t:t+``}function z(e,t){if(P(e)!=`object`||!e)return e;var n=e[Symbol.toPrimitive];if(n!==void 0){var r=n.call(e,t);if(P(r)!=`object`)return r;throw TypeError(`@@toPrimitive must return a primitive value.`)}return(t===`string`?String:Number)(e)}var B=[`data-p`],V=[`data-p`],H=[`data-p`],U=[`aria-label`,`data-p`],W=[`data-p`];function G(e,n,a,o,s,l){var p=f(`TimesIcon`),y=g(`ripple`);return u(),t(m,v({name:`p-message`,appear:``},e.ptmi(`transition`)),{default:S(function(){return[s.visible?(u(),_(`div`,v({key:0,class:e.cx(`root`),role:`alert`,"aria-live":`assertive`,"aria-atomic":`true`,"data-p":l.dataP},e.ptm(`root`)),[c(`div`,v({class:e.cx(`contentWrapper`)},e.ptm(`contentWrapper`)),[e.$slots.container?d(e.$slots,`container`,{key:0,closeCallback:l.close}):(u(),_(`div`,v({key:1,class:e.cx(`content`),"data-p":l.dataP},e.ptm(`content`)),[d(e.$slots,`icon`,{class:i(e.cx(`icon`))},function(){return[(u(),t(h(e.icon?`span`:null),v({class:[e.cx(`icon`),e.icon],"data-p":l.dataP},e.ptm(`icon`)),null,16,[`class`,`data-p`]))]}),e.$slots.default?(u(),_(`div`,v({key:0,class:e.cx(`text`),"data-p":l.dataP},e.ptm(`text`)),[d(e.$slots,`default`)],16,H)):r(``,!0),e.closable?te((u(),_(`button`,v({key:1,class:e.cx(`closeButton`),"aria-label":l.closeAriaLabel,type:`button`,onClick:n[0]||=function(e){return l.close(e)},"data-p":l.dataP},I(I({},e.closeButtonProps),e.ptm(`closeButton`))),[d(e.$slots,`closeicon`,{},function(){return[e.closeIcon?(u(),_(`i`,v({key:0,class:[e.cx(`closeIcon`),e.closeIcon],"data-p":l.dataP},e.ptm(`closeIcon`)),null,16,W)):(u(),t(p,v({key:1,class:[e.cx(`closeIcon`),e.closeIcon],"data-p":l.dataP},e.ptm(`closeIcon`)),null,16,[`class`,`data-p`]))]})],16,U)),[[y]]):r(``,!0)],16,V))],16)],16,B)):r(``,!0)]}),_:3},16)}N.render=G;var K=[`amber`,`cinder`,`ember`,`indigo`,`lumen`,`moss`,`nova`,`onyx`],q=[`badger`,`falcon`,`fox`,`otter`,`signal`,`sparrow`,`voyager`,`warden`],ae=[`admin`,`analyst`,`maintainer`,`operator`,`reviewer`];function J(e){return e[Math.floor(Math.random()*e.length)]}function Y(e){return`'${e.replaceAll(`'`,`'"'"'`)}'`}function oe(){return`${J(K)}-${J(q)}-${Math.floor(Math.random()*900+100)}`}function se(e){let t={user:{username:oe(),role:J(ae)},datetime:new Date().toISOString(),event_type:`test`};return[`curl -X POST ${Y(e)} \\`,`  -H ${Y(`Content-Type: application/json`)} \\`,`  -H ${Y(`Accept: application/json`)} \\`,`  --data-raw ${Y(JSON.stringify(t))}`].join(`
`)}async function ce(e){try{if(navigator.clipboard&&window.isSecureContext)return await navigator.clipboard.writeText(e),!0}catch{}try{let t=document.createElement(`textarea`);t.value=e,t.setAttribute(`readonly`,``),t.style.position=`fixed`,t.style.top=`-1000px`,t.style.opacity=`0`,document.body.appendChild(t),t.select(),t.setSelectionRange(0,e.length);let n=document.execCommand(`copy`);return document.body.removeChild(t),n}catch{return!1}}var le=[`viewBox`],ue=[`id`],de=[`id`],fe=[`y1`,`x2`,`y2`],pe=[`y1`,`x2`,`y2`],me=[`d`,`fill`],he=[`points`,`stroke`],ge=[`cx`,`cy`],X=[`d`],_e={key:0,class:`empty-label`},Z=240,Q=8,$=8,ve=b(a({__name:`TrafficSparkline`,props:{bars:{},variant:{default:`compact`},emptyLabel:{default:`Waiting for traffic`}},setup(t){let a=t,l=o(()=>{switch(a.variant){case`hero`:return 56;case`detail`:return 64;default:return 42}}),d=`traffic-spark-${e()?.uid??Math.random().toString(36).slice(2,9)}`,f=o(()=>a.bars.some(e=>e.value>0)),p=o(()=>l.value-$),m=o(()=>{let e=Math.max(a.bars.length-1,1);return a.bars.map((t,n)=>({x:Q+(Z-Q*2)*n/e,y:p.value-t.ratio*(l.value-$*2)}))}),h=o(()=>m.value.map(e=>`${e.x},${e.y}`).join(` `)),g=o(()=>{if(!m.value.length)return``;let e=m.value[0],t=m.value[m.value.length-1];return`${m.value.map((e,t)=>`${t===0?`M`:`L`} ${e.x} ${e.y}`).join(` `)} L ${t.x} ${p.value} L ${e.x} ${p.value} Z`}),v=o(()=>Array.from({length:3},(e,t)=>p.value-(l.value-$*2)/4*(t+1))),y=o(()=>{let e=l.value/2;return[`M ${Q} ${e}`,`C ${Q+24} ${e-10}, ${Q+42} ${e+10}, ${Q+66} ${e}`,`S ${Q+108} ${e-10}, ${Q+132} ${e}`,`S ${Q+174} ${e+10}, ${Q+198} ${e}`,`S ${Q+222} ${e-10}, ${Z-Q} ${e}`].join(` `)});return(e,a)=>(u(),_(`div`,{class:i([`traffic-sparkline`,[`variant-${t.variant}`,{empty:!f.value}]])},[(u(),_(`svg`,{viewBox:`0 0 ${Z} ${l.value}`,preserveAspectRatio:`none`,"aria-hidden":`true`},[c(`defs`,null,[c(`linearGradient`,{id:`${d}-stroke`,x1:`0%`,y1:`0%`,x2:`100%`,y2:`0%`},[...a[0]||=[c(`stop`,{offset:`0%`,"stop-color":`var(--info)`,"stop-opacity":`0.88`},null,-1),c(`stop`,{offset:`100%`,"stop-color":`var(--accent)`,"stop-opacity":`0.92`},null,-1)]],8,ue),c(`linearGradient`,{id:`${d}-fill`,x1:`0%`,y1:`0%`,x2:`0%`,y2:`100%`},[...a[1]||=[c(`stop`,{offset:`0%`,"stop-color":`var(--info)`,"stop-opacity":`0.22`},null,-1),c(`stop`,{offset:`100%`,"stop-color":`var(--info)`,"stop-opacity":`0.03`},null,-1)]],8,de)]),(u(!0),_(s,null,ee(v.value,e=>(u(),_(`line`,{key:e,class:`grid-line`,x1:Q,y1:e,x2:Z-Q,y2:e},null,8,fe))),128)),c(`line`,{class:`baseline`,x1:Q,y1:p.value,x2:Z-Q,y2:p.value},null,8,pe),f.value?(u(),_(s,{key:0},[c(`path`,{class:`area`,d:g.value,fill:`url(#${d}-fill)`},null,8,me),c(`polyline`,{class:`line`,points:h.value,stroke:`url(#${d}-stroke)`},null,8,he),m.value.length?(u(),_(`circle`,{key:0,class:`end-cap`,cx:m.value[m.value.length-1].x,cy:m.value[m.value.length-1].y,r:`3.5`},null,8,ge)):r(``,!0)],64)):(u(),_(`path`,{key:1,class:`empty-wave`,d:y.value},null,8,X))],8,le)),f.value?r(``,!0):(u(),_(`span`,_e,n(t.emptyLabel),1))],2))}}),[[`__scopeId`,`data-v-81dbb1ed`]]);export{O as a,N as i,se as n,E as o,ce as r,w as s,ve as t};
//# sourceMappingURL=TrafficSparkline-Bzi_ITna.js.map