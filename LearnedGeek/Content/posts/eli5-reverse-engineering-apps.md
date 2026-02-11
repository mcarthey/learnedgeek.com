# ELI5: How Do People Figure Out What Apps Are Doing?

---

## The Pizza Box Analogy

Imagine you order a pizza. It arrives in a sealed box. You can eat the pizza — that's what it's for. But what if you wanted to know the recipe?

You could:
1. **Open the box and look** — See the toppings, smell the ingredients, maybe identify the cheese
2. **Watch the delivery** — Follow the driver back to the pizza shop and see how they make it
3. **Read the receipt** — Check what was ordered and from where

That's basically what reverse engineering is. You already have the product (the app, the game, the pizza). You're just figuring out how it was made.

## Every App Is a Zip File in a Costume

Here's a fun secret: when you download an app on your phone, you're downloading a single file — like a ZIP file — that contains everything the app needs. The code, the images, the sounds, the data.

Think of it like a suitcase. The airline (Google Play / App Store) delivers it to your phone. You usually just use what's inside. But nothing stops you from opening the suitcase and looking at what's packed.

For Android apps, the file ends in `.apk` — and you can literally rename it to `.zip` and unzip it with any computer. Inside, you'll find:

- The **code** that makes the app work
- The **images** and sounds you see and hear
- The **data tables** — like a spreadsheet of every item, character, or setting in the app

For games, those data tables are the interesting part. They contain all the numbers the game uses but never shows you.

## The Secret Recipe Book

Games hide a lot. The tooltips say things like "increases attack" or "powerful hero." But somewhere inside that ZIP file are the actual numbers: *how much* does the attack increase? *How much* more powerful?

Finding those numbers is like finding the recipe book inside the pizza box. The game developers wrote everything down — hero stats, damage formulas, bonus multipliers — they just packed it inside the app where they thought you wouldn't look.

Some of this data is easy to read (plain text, like a note on the counter). Some is in a special format (compiled code, like a recipe written in shorthand). But with the right tools — free ones you can download — you can usually read it.

## Listening to the Conversation

When you play an online game, your phone is constantly talking to a server. "I want to fight in the arena." "Here are your rewards." "Your friend sent you a gift."

This conversation happens over the internet, and with the right setup, you can listen in — like tuning into a walkie-talkie frequency. You don't change anything. You just... hear what's being said.

What does the conversation look like? It's usually not English. It's more like a code:

```
Phone:  "Hey, message type 42, here's my player ID"
Server: "Got it, here are 6 arena opponents with their stats"
Phone:  "Fight opponent #3"
Server: "You won! Here are your rewards"
```

By watching this back-and-forth, you can learn:
- What data the game sends and receives
- How features work behind the scenes
- Whether the game is sharing more about you than you'd expect

## Speaking the Language

Once you understand the conversation, the natural next step is: what if *you* could talk to the server directly?

This is like learning enough of a language to order food. You don't need to be fluent — you just need to know the right phrases. "I'd like to collect my mail." "Fight this arena opponent." "Claim my daily reward."

People build small programs (scripts) that speak the game's language. These scripts can do things like:
- Collect daily rewards automatically
- Check game data without opening the app
- Analyze your account stats in ways the game doesn't show you

## Why Does This Matter?

Reverse engineering isn't just about games. The same techniques help with:

- **Security research** — Finding out if an app is leaking your personal data
- **Interoperability** — Making apps work together when the developer didn't plan for it
- **Accessibility** — Building tools for people whose needs the original developers didn't consider
- **Education** — Understanding how software really works, not just how it looks

When security researchers find that a banking app is sending your password in plain text, they found it the same way — by looking at what the app was saying to its server.

## The Takeaway

Every app on your phone is a package you can open. Every online feature is a conversation you can listen to. The tools to do it are free, and the skills transfer to real-world security work.

The pizza box isn't sealed. It's just closed. And sometimes, looking at the recipe teaches you more than just eating the pizza.

---

*This is part of the ELI5 series — technical concepts explained without the jargon. Want the deep dive? Read the [5-part reverse engineering series](/Blog/Post/reverse-engineering-game-part-1) that covers the full journey from APK to automation.*

*Your apps are more transparent than you think.*
