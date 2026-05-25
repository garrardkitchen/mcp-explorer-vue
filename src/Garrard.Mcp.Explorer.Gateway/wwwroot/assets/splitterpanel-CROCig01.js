import{At as e,D as t,G as n,Gt as r,H as i,Kt as a,Vt as o,Wt as s,cn as c,dn as l,ft as u,h as d,ht as f,mn as p,q as m,qt as h,rn as g,u as _,un as v,vt as y,y as b}from"./_plugin-vue_export-helper-DLCbmiWh.js";var x=b.extend({name:`splitter`,style:`
    .p-splitter {
        display: flex;
        flex-wrap: nowrap;
        border: 1px solid dt('splitter.border.color');
        background: dt('splitter.background');
        border-radius: dt('border.radius.md');
        color: dt('splitter.color');
    }

    .p-splitter-vertical {
        flex-direction: column;
    }

    .p-splitter-gutter {
        flex-grow: 0;
        flex-shrink: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 1;
        background: dt('splitter.gutter.background');
    }

    .p-splitter-gutter-handle {
        border-radius: dt('splitter.handle.border.radius');
        background: dt('splitter.handle.background');
        transition:
            outline-color dt('splitter.transition.duration'),
            box-shadow dt('splitter.transition.duration');
        outline-color: transparent;
    }

    .p-splitter-gutter-handle:focus-visible {
        box-shadow: dt('splitter.handle.focus.ring.shadow');
        outline: dt('splitter.handle.focus.ring.width') dt('splitter.handle.focus.ring.style') dt('splitter.handle.focus.ring.color');
        outline-offset: dt('splitter.handle.focus.ring.offset');
    }

    .p-splitter-horizontal.p-splitter-resizing {
        cursor: col-resize;
        user-select: none;
    }

    .p-splitter-vertical.p-splitter-resizing {
        cursor: row-resize;
        user-select: none;
    }

    .p-splitter-horizontal > .p-splitter-gutter > .p-splitter-gutter-handle {
        height: dt('splitter.handle.size');
        width: 100%;
    }

    .p-splitter-vertical > .p-splitter-gutter > .p-splitter-gutter-handle {
        width: dt('splitter.handle.size');
        height: 100%;
    }

    .p-splitter-horizontal > .p-splitter-gutter {
        cursor: col-resize;
    }

    .p-splitter-vertical > .p-splitter-gutter {
        cursor: row-resize;
    }

    .p-splitterpanel {
        flex-grow: 1;
        overflow: hidden;
    }

    .p-splitterpanel-nested {
        display: flex;
    }

    .p-splitterpanel .p-splitter {
        flex-grow: 1;
        min-width: 0;
        min-height: 0;
        border: 0 none;
    }
`,classes:{root:function(e){return[`p-splitter p-component`,`p-splitter-`+e.props.layout]},gutter:`p-splitter-gutter`,gutterHandle:`p-splitter-gutter-handle`}}),S={name:`BaseSplitter`,extends:_,props:{layout:{type:String,default:`horizontal`},gutterSize:{type:Number,default:4},stateKey:{type:String,default:null},stateStorage:{type:String,default:`session`},step:{type:Number,default:5}},style:x,provide:function(){return{$pcSplitter:this,$parentInstance:this}}};function C(e){"@babel/helpers - typeof";return C=typeof Symbol==`function`&&typeof Symbol.iterator==`symbol`?function(e){return typeof e}:function(e){return e&&typeof Symbol==`function`&&e.constructor===Symbol&&e!==Symbol.prototype?`symbol`:typeof e},C(e)}function w(e,t,n){return(t=T(t))in e?Object.defineProperty(e,t,{value:n,enumerable:!0,configurable:!0,writable:!0}):e[t]=n,e}function T(e){var t=E(e,`string`);return C(t)==`symbol`?t:t+``}function E(e,t){if(C(e)!=`object`||!e)return e;var n=e[Symbol.toPrimitive];if(n!==void 0){var r=n.call(e,t);if(C(r)!=`object`)return r;throw TypeError(`@@toPrimitive must return a primitive value.`)}return(t===`string`?String:Number)(e)}function D(e){return j(e)||A(e)||k(e)||O()}function O(){throw TypeError(`Invalid attempt to spread non-iterable instance.
In order to be iterable, non-array objects must have a [Symbol.iterator]() method.`)}function k(e,t){if(e){if(typeof e==`string`)return M(e,t);var n={}.toString.call(e).slice(8,-1);return n===`Object`&&e.constructor&&(n=e.constructor.name),n===`Map`||n===`Set`?Array.from(e):n===`Arguments`||/^(?:Ui|I)nt(?:8|16|32)(?:Clamped)?Array$/.test(n)?M(e,t):void 0}}function A(e){if(typeof Symbol<`u`&&e[Symbol.iterator]!=null||e[`@@iterator`]!=null)return Array.from(e)}function j(e){if(Array.isArray(e))return M(e)}function M(e,t){(t==null||t>e.length)&&(t=e.length);for(var n=0,r=Array(t);n<t;n++)r[n]=e[n];return r}var N={name:`Splitter`,extends:S,inheritAttrs:!1,emits:[`resizestart`,`resizeend`,`resize`],dragging:!1,mouseMoveListener:null,mouseUpListener:null,touchMoveListener:null,touchEndListener:null,size:null,gutterElement:null,startPos:null,prevPanelElement:null,nextPanelElement:null,nextPanelSize:null,prevPanelSize:null,panelSizes:null,prevPanelIndex:null,timer:null,data:function(){return{prevSize:null}},mounted:function(){this.initializePanels()},beforeUnmount:function(){this.clear(),this.unbindMouseListeners()},methods:{isSplitterPanel:function(e){return e.type.name===`SplitterPanel`},initializePanels:function(){var t=this;if(this.panels&&this.panels.length){var n=!1;if(this.isStateful()&&(n=this.restoreState()),!n){var r=D(this.$el.children).filter(function(e){return e.getAttribute(`data-pc-name`)===`splitterpanel`}),i=[];this.panels.map(function(n,a){var o=(n.props&&e(n.props.size)?n.props.size:null)??100/t.panels.length;i[a]=o,r[a].style.flexBasis=`calc(`+o+`% - `+(t.panels.length-1)*t.gutterSize+`px)`}),this.panelSizes=i,this.prevSize=parseFloat(i[0]).toFixed(4)}}},onResizeStart:function(e,r,a){this.gutterElement=e.currentTarget||e.target.parentElement,this.size=this.horizontal?i(this.$el):n(this.$el),a||(this.dragging=!0,this.startPos=this.layout===`horizontal`?e.pageX||e.changedTouches[0].pageX:e.pageY||e.changedTouches[0].pageY),this.prevPanelElement=this.gutterElement.previousElementSibling,this.nextPanelElement=this.gutterElement.nextElementSibling,a?(this.prevPanelSize=this.horizontal?u(this.prevPanelElement,!0):t(this.prevPanelElement,!0),this.nextPanelSize=this.horizontal?u(this.nextPanelElement,!0):t(this.nextPanelElement,!0)):(this.prevPanelSize=100*(this.horizontal?u(this.prevPanelElement,!0):t(this.prevPanelElement,!0))/this.size,this.nextPanelSize=100*(this.horizontal?u(this.nextPanelElement,!0):t(this.nextPanelElement,!0))/this.size),this.prevPanelIndex=r,this.$emit(`resizestart`,{originalEvent:e,sizes:this.panelSizes}),this.$refs.gutter[r].setAttribute(`data-p-gutter-resizing`,!0),this.$el.setAttribute(`data-p-resizing`,!0)},onResize:function(e,t,n){var r,i,a;n?this.horizontal?(i=100*(this.prevPanelSize+t)/this.size,a=100*(this.nextPanelSize-t)/this.size):(i=100*(this.prevPanelSize-t)/this.size,a=100*(this.nextPanelSize+t)/this.size):(r=this.horizontal?m(this.$el)?(this.startPos-e.pageX)*100/this.size:(e.pageX-this.startPos)*100/this.size:(e.pageY-this.startPos)*100/this.size,i=this.prevPanelSize+r,a=this.nextPanelSize-r),this.validateResize(i,a)||(i=Math.min(Math.max(this.prevPanelMinSize,i),100-this.nextPanelMinSize),a=Math.min(Math.max(this.nextPanelMinSize,a),100-this.prevPanelMinSize)),this.prevPanelElement.style.flexBasis=`calc(`+i+`% - `+(this.panels.length-1)*this.gutterSize+`px)`,this.nextPanelElement.style.flexBasis=`calc(`+a+`% - `+(this.panels.length-1)*this.gutterSize+`px)`,this.panelSizes[this.prevPanelIndex]=i,this.panelSizes[this.prevPanelIndex+1]=a,this.prevSize=parseFloat(i).toFixed(4),this.$emit(`resize`,{originalEvent:e,sizes:this.panelSizes})},onResizeEnd:function(e){this.isStateful()&&this.saveState(),this.$emit(`resizeend`,{originalEvent:e,sizes:this.panelSizes}),this.$refs.gutter.forEach(function(e){return e.setAttribute(`data-p-gutter-resizing`,!1)}),this.$el.setAttribute(`data-p-resizing`,!1),this.clear()},repeat:function(e,t,n){this.onResizeStart(e,t,!0),this.onResize(e,n,!0)},setTimer:function(e,t,n){var r=this;this.timer||=setInterval(function(){r.repeat(e,t,n)},40)},clearTimer:function(){this.timer&&=(clearInterval(this.timer),null)},onGutterKeyUp:function(){this.clearTimer(),this.onResizeEnd()},onGutterKeyDown:function(e,t){switch(e.code){case`ArrowLeft`:this.layout===`horizontal`&&this.setTimer(e,t,this.step*-1),e.preventDefault();break;case`ArrowRight`:this.layout===`horizontal`&&this.setTimer(e,t,this.step),e.preventDefault();break;case`ArrowDown`:this.layout===`vertical`&&this.setTimer(e,t,this.step*-1),e.preventDefault();break;case`ArrowUp`:this.layout===`vertical`&&this.setTimer(e,t,this.step),e.preventDefault();break}},onGutterMouseDown:function(e,t){this.onResizeStart(e,t),this.bindMouseListeners()},onGutterTouchStart:function(e,t){this.onResizeStart(e,t),this.bindTouchListeners(),e.preventDefault()},onGutterTouchMove:function(e){this.onResize(e),e.preventDefault()},onGutterTouchEnd:function(e){this.onResizeEnd(e),this.unbindTouchListeners(),e.preventDefault()},bindMouseListeners:function(){var e=this;this.mouseMoveListener||(this.mouseMoveListener=function(t){return e.onResize(t)},document.addEventListener(`mousemove`,this.mouseMoveListener)),this.mouseUpListener||(this.mouseUpListener=function(t){e.onResizeEnd(t),e.unbindMouseListeners()},document.addEventListener(`mouseup`,this.mouseUpListener))},bindTouchListeners:function(){var e=this;this.touchMoveListener||(this.touchMoveListener=function(t){return e.onResize(t.changedTouches[0])},document.addEventListener(`touchmove`,this.touchMoveListener)),this.touchEndListener||(this.touchEndListener=function(t){e.resizeEnd(t),e.unbindTouchListeners()},document.addEventListener(`touchend`,this.touchEndListener))},validateResize:function(e,t){return!(e>100||e<0||t>100||t<0||this.prevPanelMinSize>e||this.nextPanelMinSize>t)},unbindMouseListeners:function(){this.mouseMoveListener&&=(document.removeEventListener(`mousemove`,this.mouseMoveListener),null),this.mouseUpListener&&=(document.removeEventListener(`mouseup`,this.mouseUpListener),null)},unbindTouchListeners:function(){this.touchMoveListener&&=(document.removeEventListener(`touchmove`,this.touchMoveListener),null),this.touchEndListener&&=(document.removeEventListener(`touchend`,this.touchEndListener),null)},clear:function(){this.dragging=!1,this.size=null,this.startPos=null,this.prevPanelElement=null,this.nextPanelElement=null,this.prevPanelSize=null,this.nextPanelSize=null,this.gutterElement=null,this.prevPanelIndex=null},isStateful:function(){return this.stateKey!=null},getStorage:function(){switch(this.stateStorage){case`local`:return window.localStorage;case`session`:return window.sessionStorage;default:throw Error(this.stateStorage+` is not a valid value for the state storage, supported values are "local" and "session".`)}},saveState:function(){y(this.panelSizes)&&this.getStorage().setItem(this.stateKey,JSON.stringify(this.panelSizes))},restoreState:function(){var e=this,t=this.getStorage().getItem(this.stateKey);return t?(this.panelSizes=JSON.parse(t),D(this.$el.children).filter(function(e){return e.getAttribute(`data-pc-name`)===`splitterpanel`}).forEach(function(t,n){t.style.flexBasis=`calc(`+e.panelSizes[n]+`% - `+(e.panels.length-1)*e.gutterSize+`px)`}),!0):!1},resetState:function(){this.initializePanels()}},computed:{panels:function(){var e=this,t=[];return this.$slots.default().forEach(function(n){e.isSplitterPanel(n)?t.push(n):n.children instanceof Array&&n.children.forEach(function(n){e.isSplitterPanel(n)&&t.push(n)})}),t},gutterStyle:function(){return this.horizontal?{width:this.gutterSize+`px`}:{height:this.gutterSize+`px`}},horizontal:function(){return this.layout===`horizontal`},getPTOptions:function(){return{context:{nested:this.$parentInstance?.nestedState}}},prevPanelMinSize:function(){var e=d(this.panels[this.prevPanelIndex],`minSize`);return this.panels[this.prevPanelIndex].props&&e?e:0},nextPanelMinSize:function(){var e=d(this.panels[this.prevPanelIndex+1],`minSize`);return this.panels[this.prevPanelIndex+1].props&&e?e:0},dataP:function(){return f(w(w({},this.layout,this.layout),`nested`,this.$parentInstance?.nestedState!=null))}}},P=[`data-p`],F=[`onMousedown`,`onTouchstart`,`onTouchmove`,`onTouchend`,`data-p`],I=[`aria-orientation`,`aria-valuenow`,`onKeydown`,`data-p`];function L(e,t,n,i,l,u){return c(),h(`div`,g({class:e.cx(`root`),"data-p-resizing":!1,"data-p":u.dataP},e.ptmi(`root`,u.getPTOptions)),[(c(!0),h(o,null,v(u.panels,function(n,i){return c(),h(o,{key:i},[(c(),r(p(n),{tabindex:`-1`})),i===u.panels.length-1?a(``,!0):(c(),h(`div`,g({key:0,ref_for:!0,ref:`gutter`,class:e.cx(`gutter`),role:`separator`,tabindex:`-1`,onMousedown:function(e){return u.onGutterMouseDown(e,i)},onTouchstart:function(e){return u.onGutterTouchStart(e,i)},onTouchmove:function(e){return u.onGutterTouchMove(e,i)},onTouchend:function(e){return u.onGutterTouchEnd(e,i)},"data-p-gutter-resizing":!1,"data-p":u.dataP},{ref_for:!0},e.ptm(`gutter`)),[s(`div`,g({class:e.cx(`gutterHandle`),tabindex:`0`,style:[u.gutterStyle],"aria-orientation":e.layout,"aria-valuenow":l.prevSize,onKeyup:t[0]||=function(){return u.onGutterKeyUp&&u.onGutterKeyUp.apply(u,arguments)},onKeydown:function(e){return u.onGutterKeyDown(e,i)},"data-p":u.dataP},{ref_for:!0},e.ptm(`gutterHandle`)),null,16,I)],16,F))],64)}),128))],16,P)}N.render=L;var R=b.extend({name:`splitterpanel`,classes:{root:function(e){var t=e.instance;return[`p-splitterpanel`,{"p-splitterpanel-nested":t.isNested}]}}}),z={name:`SplitterPanel`,extends:{name:`BaseSplitterPanel`,extends:_,props:{size:{type:Number,default:null},minSize:{type:Number,default:null}},style:R,provide:function(){return{$pcSplitterPanel:this,$parentInstance:this}}},inheritAttrs:!1,data:function(){return{nestedState:null}},computed:{isNested:function(){var e=this;return this.$slots.default().some(function(t){return e.nestedState=t.type.name===`Splitter`?!0:null,e.nestedState})},getPTOptions:function(){return{context:{nested:this.isNested}}}}};function B(e,t,n,r,i,a){return c(),h(`div`,g({ref:`container`,class:e.cx(`root`)},e.ptmi(`root`,a.getPTOptions)),[l(e.$slots,`default`)],16)}z.render=B;export{N as n,z as t};
//# sourceMappingURL=splitterpanel-CROCig01.js.map