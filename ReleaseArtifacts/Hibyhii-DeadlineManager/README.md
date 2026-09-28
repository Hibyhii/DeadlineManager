### DeadlineManager



DeadlineManager gives the host control over how many days the crew gets for each quota in *Lethal Company*.



Choose a fixed deadline, scale it linearly as quotas are completed, use a quadratic growth curve, or let the mod adjust the deadline based on how the crew has been performing.

#### 

#### Features



\- Static deadlines — always use the same number of days.

\- Linear scaling — add a configurable amount of time for each completed quota.

\- Quadratic scaling — gradually increase the deadline using a configurable quadratic curve.

\- Dynamic adjustment — optionally give more time to an under-performing crew and less time to an over-performing crew.

\- Configurable floor and upper clamp for Linear and Quadratic modes.

\- Per-save performance history so dynamic adjustment survives restarts.

\- Host-authoritative behavior — only the host/server calculates and applies deadline changes.

\- Existing-save reconciliation — changing the initial deadline preserves time already elapsed where possible.

\- Reset handling — new/reset saves and ship resets receive the configured starting deadline.

\- Challenge files are ignored.



#### Deadline Modes



##### Static



Always uses `Static Deadline Days`.



Static mode ignores the shared deadline floor, upper clamp, and dynamic adjustment settings.

##### 

##### Linear



The deadline grows directly with completed quotas:



text

Deadline = Initial Floor + (Days Added Per Quota × Quotas Completed)





Fractional growth is calculated from the total number of completed quotas before the final deadline is rounded, so rounding does not accumulate between quotas.



##### Quadratic



The deadline follows this curve:



text

Deadline = Initial Floor + Quadratic Growth × (Quotas Completed² / 16)





This is the default baseline mode.



#### Dynamic Adjustment



Dynamic adjustment can be enabled for Linear or Quadratic mode.



The mod records how far above or below each required quota the crew finished:



text

Performance = (Quota Fulfilled - Quota Required) / Quota Required





The first 3 completed quotas are used as calibration. Starting with the next deadline, the mod compares recent performance against that calibration baseline.



\- Finishing below the calibrated performance level applies \*\*Upward Pressure\*\*, increasing the deadline.

\- Finishing above the calibrated performance level applies \*\*Downward Pressure\*\*, reducing the deadline.

\- After calibration, up to the \*\*3 most recent quotas\*\* are used, with newer results weighted more heavily.

\- Setting either pressure value to `0` disables adjustment in that direction.



Dynamic adjustment is applied to the selected Linear or Quadratic baseline and then constrained by the configured floor and optional upper clamp.





