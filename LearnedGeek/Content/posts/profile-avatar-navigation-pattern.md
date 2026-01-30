Here's a common mobile navigation problem: you have 3-4 navigation destinations, but one of them (Settings) is clearly less important than the others. A 4-tab bottom nav wastes prime thumb-zone real estate on a rarely-used option. But hiding it in a hamburger menu makes it too hidden.

The solution: put a profile avatar in the header that leads to settings.

## The Pattern

```
┌─────────────────────────────────────────┐
│                                    [MJ] │ ← Tappable avatar
│  Good morning,                          │
│  Mike                                   │
│                                         │
├─────────────────────────────────────────┤
│                                         │
│         Main content                    │
│                                         │
├─────────────────────────────────────────┤
│                                         │
│   📋            ⏱️                      │
│   My Work       Time                    │ ← Primary navigation
│                                         │
└─────────────────────────────────────────┘
```

The avatar sits in the header—visible on every page—and tapping it navigates to a Settings/Profile page. The bottom nav is reserved for primary actions only.

## Why This Works

### 1. Visual Presence Without Competition

The avatar is always visible, so users know it exists. But it's in the header, not competing with primary navigation in the thumb zone.

### 2. Personalization

Showing the user's initials makes the app feel personalized. "MJ" is more human than a gear icon. It's a small touch that says "this is your app."

### 3. Natural Affordance

Avatars are universally understood as clickable elements that lead to profile/settings. Users don't need to learn this—they've seen it in every social app they use.

### 4. Scalability

If you later add profile customization (photo upload, display name changes), the avatar already leads there. The navigation pattern supports future features.

## The Implementation

### Razor Component

```razor
<div class="m3-page-header">
    <button class="m3-profile-avatar m3-profile-avatar--header"
            @onclick="NavigateToProfile">
        @User.FirstName[0]@User.LastName[0]
    </button>

    <p class="m3-page-header__greeting">@GetGreeting()</p>
    <h1 class="m3-page-header__title">@User.FirstName</h1>
</div>

@code {
    private void NavigateToProfile()
    {
        Navigation.NavigateTo("/more");
    }
}
```

### CSS

```css
.m3-profile-avatar {
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    font-weight: 600;
    text-transform: uppercase;
    border: none;
    cursor: pointer;
    transition: transform 0.2s ease;
}

.m3-profile-avatar:active {
    transform: scale(0.92);
}

.m3-profile-avatar--header {
    position: absolute;
    top: 20px;
    right: 16px;
    width: 44px;
    height: 44px;
    font-size: 14px;
    background-color: var(--m3-tertiary);
    color: var(--m3-on-tertiary);
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
    z-index: 10;
}
```

The 44px size meets M3's touch target guidelines while not being so large it dominates the header.

## Positioning Considerations

### With Gradient Headers

If your header has a gradient background, the avatar needs contrast. Use a solid background color for the avatar and a subtle shadow:

```css
.m3-profile-avatar--header {
    background-color: #6D4C7D;  /* Contrasts with gradient */
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}
```

### With Floating Card Design

If you're using a floating card pattern (where the app is inset from screen edges), push the avatar down to clear the rounded corner:

```css
.m3-profile-avatar--header {
    top: 20px;  /* Clears 24px corner radius */
    right: 16px;
}
```

### With Safe Areas

If you need to account for notches or status bars:

```css
.m3-profile-avatar--header {
    top: calc(20px + env(safe-area-inset-top));
}
```

## The Settings Page

When the avatar is tapped, navigate to a Settings/Profile/More page:

```razor
@page "/more"

<div class="m3-profile-hero">
    <div class="m3-profile-hero__avatar">
        @User.FirstName[0]@User.LastName[0]
    </div>
    <div class="m3-profile-hero__info">
        <div class="m3-profile-hero__name">@User.FullName</div>
        <div class="m3-profile-hero__role">@User.Role</div>
    </div>
</div>

<div class="m3-settings-group">
    <div class="m3-settings-item">My Profile</div>
    <div class="m3-settings-item">Notifications</div>
    <div class="m3-settings-item">App Settings</div>
</div>

<div class="m3-signout-card">
    Sign Out
</div>
```

The profile page starts with a larger version of the avatar, reinforcing the connection. Settings are grouped logically below.

## Accessibility Considerations

### ARIA Label

Screen readers need to know what the button does:

```razor
<button class="m3-profile-avatar m3-profile-avatar--header"
        @onclick="NavigateToProfile"
        aria-label="Open profile and settings">
    @User.FirstName[0]@User.LastName[0]
</button>
```

### Color Contrast

The initials must meet WCAG AA contrast against the avatar background. Test your color combination:

| Background | Text | Ratio | Pass? |
|------------|------|-------|-------|
| #6D4C7D (purple) | #FFFFFF | 4.7:1 | AA ✓ |
| #40B4B4 (teal) | #FFFFFF | 2.9:1 | Fail |
| #40B4B4 (teal) | #1A365D (navy) | 4.8:1 | AA ✓ |

If using a light-colored avatar, use dark text for the initials.

### Focus State

Keyboard users need a visible focus indicator:

```css
.m3-profile-avatar:focus-visible {
    outline: 3px solid var(--m3-primary);
    outline-offset: 2px;
}
```

## When Not to Use This Pattern

The profile avatar pattern works when:
- Settings/Profile is a secondary action
- Users access settings infrequently
- You have 2-4 navigation items, with one less important

Don't use it when:
- Settings is accessed frequently (make it a tab instead)
- User identity isn't meaningful (anonymous users)
- The header is already crowded with other elements

## Variations

### With Badge

Show a notification badge for pending actions:

```razor
<button class="m3-profile-avatar m3-profile-avatar--header">
    @User.Initials
    @if (PendingNotifications > 0)
    {
        <span class="m3-avatar-badge">@PendingNotifications</span>
    }
</button>
```

### With Photo

If users have profile photos:

```razor
@if (!string.IsNullOrEmpty(User.PhotoUrl))
{
    <button class="m3-profile-avatar m3-profile-avatar--header">
        <img src="@User.PhotoUrl" alt="@User.FullName" />
    </button>
}
else
{
    <button class="m3-profile-avatar m3-profile-avatar--header">
        @User.Initials
    </button>
}
```

### With Dropdown (Web)

On web/desktop, you might use a dropdown instead of navigation:

```razor
<div class="m3-avatar-dropdown">
    <button class="m3-profile-avatar" @onclick="ToggleDropdown">
        @User.Initials
    </button>
    @if (showDropdown)
    {
        <div class="m3-avatar-dropdown__menu">
            <a href="/profile">My Profile</a>
            <a href="/settings">Settings</a>
            <button @onclick="SignOut">Sign Out</button>
        </div>
    }
</div>
```

## Key Takeaways

1. **Reserve bottom nav for primary actions** — Don't waste thumb-zone space on Settings
2. **Avatar provides presence without competition** — Visible but not intrusive
3. **Initials personalize the app** — "MJ" is more human than a gear icon
4. **The pattern is learned** — Users expect avatars to lead to profile
5. **Consider accessibility** — Labels, contrast, focus states matter
6. **44px is the sweet spot** — Large enough to tap, small enough to not dominate

## Related Posts

- [Drawer vs. Bottom Nav: A UX Decision Framework](/blog/drawer-vs-bottom-nav-ux-framework) — How we arrived at this navigation structure
- [M3E for Blue-Collar Apps](/blog/m3e-design-for-field-workers) — The larger design context

---

*The best patterns feel obvious in retrospect. "Put a profile avatar in the header" is so simple it almost doesn't need a blog post. But it took us three navigation iterations to arrive at it—from hamburger menu, to 3-tab bottom nav, to 2-tab + avatar. Sometimes the obvious answer isn't obvious until you've tried the alternatives.*
