import{Gt as e,In as t,Kt as n,cn as r,dn as i,ht as a,mn as o,qt as s,rn as c,u as l,y as u}from"./_plugin-vue_export-helper-DLCbmiWh.js";import{u as d}from"./index-oMvdfyp9.js";var f=u.extend({name:`chip`,style:`
    .p-chip {
        display: inline-flex;
        align-items: center;
        background: dt('chip.background');
        color: dt('chip.color');
        border-radius: dt('chip.border.radius');
        padding-block: dt('chip.padding.y');
        padding-inline: dt('chip.padding.x');
        gap: dt('chip.gap');
    }

    .p-chip-icon {
        color: dt('chip.icon.color');
        font-size: dt('chip.icon.size');
        width: dt('chip.icon.size');
        height: dt('chip.icon.size');
    }

    .p-chip-image {
        border-radius: 50%;
        width: dt('chip.image.width');
        height: dt('chip.image.height');
        margin-inline-start: calc(-1 * dt('chip.padding.y'));
    }

    .p-chip:has(.p-chip-remove-icon) {
        padding-inline-end: dt('chip.padding.y');
    }

    .p-chip:has(.p-chip-image) {
        padding-block-start: calc(dt('chip.padding.y') / 2);
        padding-block-end: calc(dt('chip.padding.y') / 2);
    }

    .p-chip-remove-icon {
        cursor: pointer;
        font-size: dt('chip.remove.icon.size');
        width: dt('chip.remove.icon.size');
        height: dt('chip.remove.icon.size');
        color: dt('chip.remove.icon.color');
        border-radius: 50%;
        transition:
            outline-color dt('chip.transition.duration'),
            box-shadow dt('chip.transition.duration');
        outline-color: transparent;
    }

    .p-chip-remove-icon:focus-visible {
        box-shadow: dt('chip.remove.icon.focus.ring.shadow');
        outline: dt('chip.remove.icon.focus.ring.width') dt('chip.remove.icon.focus.ring.style') dt('chip.remove.icon.focus.ring.color');
        outline-offset: dt('chip.remove.icon.focus.ring.offset');
    }
`,classes:{root:`p-chip p-component`,image:`p-chip-image`,icon:`p-chip-icon`,label:`p-chip-label`,removeIcon:`p-chip-remove-icon`}}),p={name:`Chip`,extends:{name:`BaseChip`,extends:l,props:{label:{type:[String,Number],default:null},icon:{type:String,default:null},image:{type:String,default:null},removable:{type:Boolean,default:!1},removeIcon:{type:String,default:void 0}},style:f,provide:function(){return{$pcChip:this,$parentInstance:this}}},inheritAttrs:!1,emits:[`remove`],data:function(){return{visible:!0}},methods:{onKeydown:function(e){(e.key===`Enter`||e.key===`Backspace`)&&this.close(e)},close:function(e){this.visible=!1,this.$emit(`remove`,e)}},computed:{dataP:function(){return a({removable:this.removable})}},components:{TimesCircleIcon:d}},m=[`aria-label`,`data-p`],h=[`src`];function g(a,l,u,d,f,p){return f.visible?(r(),s(`div`,c({key:0,class:a.cx(`root`),"aria-label":a.label},a.ptmi(`root`),{"data-p":p.dataP}),[i(a.$slots,`default`,{},function(){return[a.image?(r(),s(`img`,c({key:0,src:a.image},a.ptm(`image`),{class:a.cx(`image`)}),null,16,h)):a.$slots.icon?(r(),e(o(a.$slots.icon),c({key:1,class:a.cx(`icon`)},a.ptm(`icon`)),null,16,[`class`])):a.icon?(r(),s(`span`,c({key:2,class:[a.cx(`icon`),a.icon]},a.ptm(`icon`)),null,16)):n(``,!0),a.label===null?n(``,!0):(r(),s(`div`,c({key:3,class:a.cx(`label`)},a.ptm(`label`)),t(a.label),17))]}),a.removable?i(a.$slots,`removeicon`,{key:0,removeCallback:p.close,keydownCallback:p.onKeydown},function(){return[(r(),e(o(a.removeIcon?`span`:`TimesCircleIcon`),c({class:[a.cx(`removeIcon`),a.removeIcon],onClick:p.close,onKeydown:p.onKeydown},a.ptm(`removeIcon`)),null,16,[`class`,`onClick`,`onKeydown`]))]}):n(``,!0)],16,m)):n(``,!0)}p.render=g;export{p as t};
//# sourceMappingURL=chip-o6ZqImx4.js.map