Android fragmentation is a real problem for mobile developers. You design a beautiful full-bleed layout, test it on a Pixel, and then see it on a Samsung budget phone with a 2-inch navigation bar eating into your carefully balanced composition.

The usual solution is `env(safe-area-inset-*)` and padding calculations. But there's another approach that's both simpler and more visually distinctive: stop fighting the system chrome and make it part of your design.

## The Problem

Modern Android devices have wildly different system UI:

```
Pixel 8 Pro:               Samsung A15:              Older Samsung:
┌─────────────────┐        ┌─────────────────┐       ┌─────────────────┐
│ thin status bar │        │ thick status bar│       │ thick status bar│
├─────────────────┤        ├─────────────────┤       ├─────────────────┤
│                 │        │                 │       │                 │
│   YOUR APP      │        │   YOUR APP      │       │   YOUR APP      │
│                 │        │                 │       │                 │
│                 │        │                 │       │                 │
├─────────────────┤        ├─────────────────┤       ├─────────────────┤
│ gesture bar     │        │ nav buttons     │       │ HUGE nav bar    │
└─────────────────┘        └─────────────────┘       └─────────────────┘
```

If you try to make your content fill the entire screen, you're constantly calculating offsets. And the calculations change between devices, between Android versions, and between gesture navigation vs. button navigation modes.

## The Floating Card Solution

What if we stopped trying to fill the screen edge-to-edge? What if we treated our app content as a card floating above a solid background?

```
┌───────────────────────────────────────────────┐
│  ████████ Status Bar ████████  (navy bg)     │
│  ╭─────────────────────────────────────────╮  │
│  │                                         │  │
│  │         App content as card             │  │
│  │         with rounded corners            │  │
│  │                                         │  │
│  │                                         │  │
│  │  ┌─────────────────────────────────┐    │  │
│  │  │  Bottom Nav (inside card)       │    │  │
│  ╰──┴─────────────────────────────────┴────╯  │
│  ████████ Nav Buttons ████████  (navy bg)    │
└───────────────────────────────────────────────┘
```

The navy background extends behind the status bar and navigation buttons. They become part of the design, not awkward intruders. The rounded corners of the card create a consistent boundary regardless of device.

## The CSS

This is surprisingly simple:

```css
/* Make the entire page navy */
html, body {
  margin: 0;
  padding: 0;
  background-color: #1A365D;
  overscroll-behavior: none;
}

/* The card that holds all app content */
.nav-scaffold {
  position: fixed;
  top: 8px;
  left: 8px;
  right: 8px;
  bottom: 8px;

  display: flex;
  flex-direction: column;

  background-color: #FAFAFA;
  border-radius: 24px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4),
              0 2px 8px rgba(0, 0, 0, 0.2);
  overflow: hidden;
}

/* Scrollable content area */
.nav-scaffold .main-content {
  flex: 1;
  overflow-y: auto;
  padding-bottom: 16px;
}

/* Bottom nav is part of the card */
.m3-bottom-nav {
  flex-shrink: 0;
  border-radius: 0 0 20px 20px;
  min-height: 80px;
  padding: 8px 16px 16px 16px;
}
```

That's it. The `position: fixed` with 8px insets creates the floating effect. The `overflow: hidden` clips the content to the rounded corners. The `flex-direction: column` stacks the scrollable content above the bottom nav.

## Why This Works

### 1. Device Agnostic

The 8px gap is the same on every device. Whether the device has a tiny gesture bar or a massive navigation button row, the gap is consistent. The system chrome sits in the navy "background" and doesn't affect your layout.

### 2. No Safe Area Calculations

You don't need to query `env(safe-area-inset-bottom)` or `Platform.GetInsets()`. The card floats above all of it. The only thing that matters is the 8px gap you've chosen.

### 3. Visual Distinction

Most apps try to blend into the system. This design creates a clear visual boundary between "your app" and "the phone's UI." The card metaphor is immediately understandable—users know they're in a distinct space.

### 4. Consistent Branding

The navy background reinforces your brand. In our case, it matches our blueprint theme. Every time the user sees that navy peeking around the edges, it's a brand touchpoint.

## The Bottom Nav Challenge

One gotcha: with a floating card, you can't use `position: fixed; bottom: 0` for your bottom navigation. That would position it relative to the viewport, not the card.

Instead, use flexbox:

```html
<div class="nav-scaffold">
  <main class="main-content">
    <!-- Scrollable content -->
  </main>

  <nav class="m3-bottom-nav">
    <!-- Bottom nav buttons -->
  </nav>
</div>
```

The `.main-content` gets `flex: 1` to fill available space. The `.m3-bottom-nav` gets `flex-shrink: 0` to maintain its height. The nav stays at the bottom of the card naturally.

Don't forget rounded bottom corners on the nav to match the card:

```css
.m3-bottom-nav {
  border-radius: 0 0 20px 20px;
}
```

## Header Positioning

With the card inset 8px from the top, your header content needs to account for that. But this is actually simpler than safe area calculations:

```css
.m3-page-header {
  padding: 48px 24px 60px 24px;
}
```

You're just padding from the top of the card, not from some device-dependent safe area. The 48px top padding works on every device.

## Profile Avatar Positioning

If you have an absolute-positioned element in the header (like a profile avatar), push it down slightly to clear the card's rounded corner:

```css
.m3-profile-avatar--header {
  position: absolute;
  top: 20px;  /* Not 12px - clear the 24px corner radius */
  right: 16px;
}
```

The exact value depends on your corner radius. For a 24px radius, 20px top offset works well.

## Preventing Overscroll Bleed

On iOS Safari and some Android browsers, overscroll shows a bounce effect that reveals what's behind your content. With a navy background and white card, this looks intentional—the navy "bleeds through" during overscroll, reinforcing the floating effect.

But to prevent any unwanted behavior:

```css
html, body {
  overscroll-behavior: none;
}
```

## When Not to Use This Pattern

The floating card pattern works well for:
- Apps with distinct branding (the gap shows brand color)
- Apps targeting diverse Android devices
- Apps where visual distinction from the OS is desirable

It's less appropriate for:
- Content-heavy apps where screen real estate is precious
- Apps that should blend seamlessly with the OS
- Apps with full-screen media (video players, cameras)

The 16px total gap (8px on each side) is non-trivial on a 320px-wide phone. If every pixel matters for your content density, this pattern costs you space.

## The Blazor Hybrid Specifics

In a MAUI Blazor Hybrid app, the WebView fills the MAUI container. The CSS above works within the WebView. To make the status bar transparent and show your navy background:

```csharp
// Platforms/Android/MainActivity.cs
protected override void OnCreate(Bundle? savedInstanceState)
{
    base.OnCreate(savedInstanceState);

    if (Window != null)
    {
        Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#1A365D"));
    }
}
```

This ensures the status bar color matches your background when the app first loads.

## Key Takeaways

1. **Stop fighting device chrome** — Make it part of your design
2. **8px fixed insets** — Works on every device, no calculations needed
3. **Flexbox for bottom nav** — Not `position: fixed`
4. **Round the corners** — The card metaphor needs consistent curves
5. **Match status bar color** — Set it in native code for seamless loading
6. **Brand reinforcement** — The gap is a branding opportunity

## The Result

We went from debugging safe area calculations across 8 different test devices to a single CSS file that works everywhere. The navy background became part of our blueprint brand identity. And users immediately understand they're in a distinct app space, not fighting with Android's UI.

Sometimes the solution to fragmentation isn't more complexity—it's embracing the constraints.

---

*The floating card pattern trades 16px of screen width for complete device independence. For our use case—a field worker app where users need large touch targets anyway—that tradeoff was obvious. Your mileage may vary.*

## Related Posts

- [M3E for Blue-Collar Apps](/blog/m3e-design-for-field-workers) — The design context that led to this pattern
- [Drawer vs. Bottom Nav: A UX Decision Framework](/blog/drawer-vs-bottom-nav-ux-framework) — Why the bottom nav is inside the card
