# Daymark / Horizon

The first Horizon identity uses a D silhouette with a rising sun and a horizontal cut. The editable SVG is `Windows/Assets/horizon-mark.svg`; `HorizonBrand.cs` uses the matching WPF geometry. `Build/Create-HorizonIcon.ps1` creates the 16–256 px Windows ICO from the same geometry. The icon uses a white mark on periwinkle; the in-app header uses a periwinkle mark on the active theme surface. Calendar colors and custom themes remain independent of brand colors.

Palette: periwinkle **#7C83FF**, warm apricot **#F3B785**, charcoal **#14161B**, ivory **#F7F6F2**. Apricot is a secondary brand color, not an event category or warning color. OLED surfaces remain pure black. Generated interface accent colors may adjust for readable contrast.

The wordmark uses the bundled **Manrope SemiBold** font. Native controls retain Segoe UI. The font is bundled as a WPF resource, never installed system-wide, and no font CDN is contacted. Source: [googlefonts/manrope](https://github.com/googlefonts/manrope/tree/468c0dbe38efa331b80bfe9448256abe27be44c3), `fonts/ttf/manrope-semibold.ttf`. Its original OFL notice is distributed in `Assets/Manrope-OFL.txt`. Retain that notice when redistributing the font.

The mark is an initial implementation of the approved concept, with room for later optical refinements. This document does not assert trademark registration or exclusivity.
