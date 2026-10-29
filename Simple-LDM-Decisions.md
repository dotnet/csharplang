# Simple LDM Decisions

## Motivation

We want to enable simple decisions, there the answer is effectively "proceed with the implementor/spec proposer's recommendation", without needing a full LDM
where reasonable. At the same time, we want to make sure that we don't surprise members of the LDM with decisions after the fact with decisions that they feel
should have been brought to the full meeting; this has effectively happened in the past with bad results, so we want to avoid it in the future. We also want to
ensure that our process, decisions, and context behind the decisions remains publicly documented, without needing to jump between a number of difference sources
in order to understand the full context behind a decision.

## Process

### Intended question scope

The questions intended to be covered by this document are "simple and obvious" decisions. This is a matter of taste, and often must be described as "I'll know it
when I see it". In general, though, the following criteria can be used as a gut check, in the eyes of the implementor or spec writer. A question does not have to
meet every one of these bullets, but it should meet the spirit of at least one of them.

* The question is extremely straightfoward, with no complex intracies to think through.
* The question has an "obviously" correct answer, where all alternatives come with extremely apparent downsides.
* The question is sufficiently corner case that we expect the main scenario that will be affected are the compiler's unit tests, with no ramifications to the
  broader feature design.

Questions brought in this manner MUST have a single proposed resolution. Alternatives can and should be presented when applicable, but there must be a recommendation
from the asker as to what should be the decision. Asking a question in this manner is effectively asking for unanimous consent for a decision.

If a member of the LDM believes a question does not meet this scope, they may request that the question be brought to a full meeting.

### Asking a question

We will use the Proposal Questions channel (to be created) in the C# Language Design Teams Team for asking questions. Questions should each go in a new thread, and
responses to that question should stay within that thread. To ask a question and propose a decision:

1. Create a thread in the channel.
    * Give it a reasonable title
    * Include a link to the championed proposal and spec for the feature. Please use a link at a specific commit: that will help you later when you need to document
      the decision.
    * Ask the question and propose the decision. Include all the relevant context for the question locally, but it is also a good idea to add the question and context
      to the spec as well.
    * Ping the channel to ensure that everyone has a notification that a question was asked.
2. Wait for feedback. You must wait at least 24 hours before considering something approved. Approval criteria is covered below.
3. Answer clarifying questions in the thread, if any are asked.
4. If approval criteria is not met due to lack of participation, you've waited at least 24 hours, and you are blocked on a decision, post a followup that you will
   consider the decision approved in another 24 hours unless someone objects. Ping the channel again to ensure that everyone sees this message.
5. If the decision is approved, you must document this:
    * Comment on the thread that the decision is considered approved.
    * Open a PR to your spec and either update the previously-added question section with the answer, including any
      relevant discussion from the thread, and indicate that the decision was made by unanimous consent.
6. If the decision is not approved, add the question to the appropriate discussion queue in the meetings README so it can be brought to a full LDM.

### Approval criteria

Approval is contingent on positive feedback, unless bullet 4 above is met. Positive feedback consists of:

* Thumbs up reactions on the question post.
* Positive comments in the thread that explicitly indicate approval.

Negative feedback consists of:

* Negative comments in the thread that explicitly indicate a request for the question to go to LDM or be rejected entirely.
    * Negative comments alone are not considered here, as we are assuming that discussion may occur in some of these threads, and tone can be hard to read via text.
      If you think something should not move forward or needs to go to LDM, pleae indicate so explicitly to avoid confusion.

You must receive a super-majority positive responses (meaning 2/3rds of the LDM approve; at time of writing, this is 8 positive feedback responses required) and no
negative feedback for the decision to be considered approved. If bullet 4 is met, you may have less than a super-majority, but you still must have no negative feedback.

### Recommendations and Asks

* Turn on notifications for all messages in the Proposal Questions channel.
* Please do take the time to give feedback. The purpose of this is to unblock folks without taking up too much time; rule 4 is something we hope to employ sparingly.
* Please be understanding as we figure out the rhythm for this approach. Give feedback early to let us know what is or isn't working.
