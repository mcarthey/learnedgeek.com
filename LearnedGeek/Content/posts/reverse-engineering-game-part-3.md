# Reverse Engineering a Mobile Game: Part 3 — Learning to Speak Binary

**Series:** Part 3 of a 5-part series. [Part 1](/Blog/Post/reverse-engineering-game-part-1) | [Part 2](/Blog/Post/reverse-engineering-game-part-2) | [Part 4](/Blog/Post/reverse-engineering-game-part-4) | [Part 5](/Blog/Post/reverse-engineering-game-part-5)

---

## The Three Unknowns

In [Part 2](/Blog/Post/reverse-engineering-game-part-2), I captured binary WebSocket frames and had the Lua source code that encodes them. Simple — just match them up, right?

Not quite. I had three things I didn't know:

1. **The packet header format** — what metadata comes before the message body?
2. **The exact field sequence** — in what order are the pieces assembled?
3. **The data types** — is a particular field 4 bytes or 8 bytes wide?

Imagine you intercepted a letter written in a language you're learning. You know some vocabulary (from the Lua source), you have a translation dictionary (the protocol definitions), but you're not sure about the grammar. Is this word a noun or a verb? Does this number mean "timestamp" or "version"?

## Starting From What I Could See

By correlating timestamps between the debug logs and captured WebSocket frames, I could match binary packets to known message types. The login message looked something like this:

```
[4 bytes] [4 bytes] [??? bytes] ...body... [??? bytes]
```

The first 4 bytes were clearly a length. The next 4 were the message ID. But what came after that? And what were those mysterious bytes at the end?

## The Wrong Guess

My first assumption: the field after the message ID was an 8-byte timestamp. The value looked plausible. I built a test client with this format, sent a login packet, and...

Rejected. The server didn't like it.

Four bytes of wrong assumption. That's all it takes when you're speaking binary.

## The Clues in the Source Code

The Lua source code had a function called `createPacket`. I could extract the string constants it referenced:

```
createPacket  writeInt32  writeInt32  writeInt32  os  time  encode  writeInt64  writeLength  getBuf
```

These are the method names and values the function uses. The presence of `os` and `time` as separate strings suggested `os.time()` was being called — which returns a 32-bit timestamp in Lua, not 64-bit.

But string extraction only tells you *what* methods are called, not *in what order* or *with what arguments*. I could see `writeInt32` and `writeInt64` both appear — but which one was the timestamp write?

## Building a Disassembler

I needed to see the actual instruction sequence. Not just the vocabulary, but the grammar. So I built a Lua 5.3 bytecode disassembler.

This sounds more dramatic than it is. Lua 5.3 bytecode has a well-documented format — 32-bit instructions with the opcode in the low bits. You really only need to understand about 10 opcodes to trace a function's logic:

| Opcode | What It Does |
|--------|-------------|
| LOADK | Load a constant value |
| GETTABUP | Look up a name in a table |
| SELF | Prepare a method call |
| CALL | Call a function |
| MOVE | Copy a register |

The critical discovery was in how Lua stores number constants. Type 3 means "float" (8 bytes). Type 19 means "integer" (8 bytes). When I first parsed the bytecode, I mixed these up — making some constants look like garbage. After fixing that, the disassembly became readable.

## The Eureka Moment

With the disassembler working, I traced the `createPacket` function step by step. The critical sequence:

```
; Step 1: Write a placeholder for the length
writeInt32(0)         -- will be filled in later

; Step 2: Write the message ID
writeInt32(msgId)     -- e.g. the login message

; Step 3: Write the timestamp -- THIS is the key!
writeInt32(os.time()) -- 32-bit, NOT 64-bit!

; Step 4: Encode and write the message body
encode(body)

; Step 5: Write a constant trailer
writeInt64(1)         -- always the value 1

; Step 6: Go back and fill in the length
writeLength()
```

There it was. The timestamp is `writeInt32` — a **32-bit** unix timestamp. Not the 64-bit value I'd assumed. And the mystery bytes at the end? A constant `Int64(1)` trailer — probably a protocol version marker that serves no visible purpose but is required for the server to accept the packet.

**Four bytes.** That was the entire difference between "rejected" and "accepted."

## The Confirmed Wire Format

With the disassembler resolving all ambiguity, the packet format was clean:

**Sending a message (client to server):**
```
[4B length][4B message_id][4B timestamp][message body][8B trailer = 1]
```

**Receiving a response (server to client):**
```
[4B length][4B message_id][4B status_code][response body]
```

All integers are big-endian. The length field counts everything *except* itself. Response status: 0 means success, negative means error, large positive means game-specific error code.

## The Moment of Truth

With the correct format, I built a login packet in Python:

```python
def build_packet(msg_id, body):
    w = MsgWriter()
    w.writeInt32(0)                  # length placeholder
    w.writeInt32(msg_id)             # message type
    w.writeInt32(int(time.time()))   # 32-bit timestamp
    w.write(body)                    # encoded message
    w.writeInt64(1)                  # trailer
    w.patch_length()                 # fill in the length
    return w.getBytes()
```

Sent it. Got back: `status=0`. **Login successful.**

Seeing your character data come back from a packet you built from scratch? That's the reverse engineering equivalent of beating a boss on the first try.

## What I Learned

1. **String extraction gets you 80% there.** The method names and constants tell you the shape of the protocol. But for the exact byte layout, you need instruction-level analysis.

2. **Lua bytecode is approachable.** The format is clean, well-documented, and you only need ~10 opcodes to trace function logic. It's not x86 assembly.

3. **Wrong assumptions compound.** My "8-byte timestamp" looked plausible enough to seem right — but was wrong enough to break everything. When speaking binary, close doesn't count.

---

In **[Part 4](/Blog/Post/reverse-engineering-game-part-4)**, I'll cover building the complete API client — authentication, server discovery, and a security model that can only be described as *optimistic*.

---

*A note on responsible disclosure: This series describes techniques for analyzing software you've downloaded for personal use. Game names, server addresses, and authentication details have been generalized. The methodology is educational — the same techniques are taught in mobile security courses and used in authorized security research. Always respect terms of service and applicable laws in your jurisdiction.*

---

*Tools used: Custom Lua 5.3 bytecode disassembler (Python), hex analysis*
