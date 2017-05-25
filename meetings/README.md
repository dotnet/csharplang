# C# Language Design Meetings

C# Language Design Meetings (LDM for short) are meetings by the C# Language Design Team and invited guests to investigate, design and ultimately decide on features to enter the C# language. It is a creative meeting, where active design work happens, not just a decision body. 

Each C# language design meeting is represented by a meeting notes file in this folder.

## Purpose of the meetings notes

Meeting notes serve the triple purposes of

- recording decisions so they can be acted upon
- communicating our design thinking to the community so we can get feedback on them
- recording rationale so we can return later and see why we did things the way we did

All have proven extremely useful over time.

## Life cycle of meeting notes

- If upcoming design meetings have a specific agenda, for instance to suit the schedule of visitors, there may be a meeting notes file with that agenda even before the meeting happens.
- Otherwise the meeting agendas are determined just-in-time based on urgency, arrival of new information or ideas, challenges found in design and implementation, and so on.
- After the meeting, notes will be saved directly here. 
- Usually they will be raw notes in need of subsequent cleaning up. If that's the case, they will be clearly marked as such, and a [Meeting notes](https://github.com/dotnet/csharplang/labels/Meeting%20notes) work item will track the task of cleaning up the notes.
- When the notes are finalized, a notification is posted as a [discussion issue](https://github.com/dotnet/csharplang/labels/Discussion) to encourage discussion of the decisions made. While quick comments are welcome directly on that issue, it is recommended to open a separate issue for deeper or more topic-specific discussions.
- If the notes impact current proposals, [proposal](https://github.com/dotnet/csharplang/labels/Proposal) work items will track updating those proposals, assigned to their [champions](https://github.com/dotnet/csharplang/labels/Proposal%20champion).
- When updated, the proposals link back to the meetings where the proposal was discussed.

## Style of design notes

The notes serve as the collective voice of the LDM. They cover not just the decisions but the discussion, options and rationale, so that others can follow along in the discussion and provide input to it, and so that we don't forget them for later.

However, *the notes are not minutes*! They *never* state who said what in the meeting. They will occasionally mention people by name if they are visiting, provided input, should be collaborated with, etc. But the notes aim to represent the shared thinking of the room. If there's disagreement, they will report that, but they won't focus on who wants what.

This approach is intended to reinforce that the LDMs are a safe space, and a collaborative, creative effort. It is not a negotiation between representatives of different interests. It is not a voting body, and it is not a venue for posturing. Everybody cooperates towards the same end: creating the best language for today's and tomorrow's C# developers.

