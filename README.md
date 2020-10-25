# Plagued

<p align="center">
  <img src="https://i.imgur.com/3JcqsKO.png?1"/>
</p>

## Plagued - Everyone is infected (COVID-19 Edition)
An unknown airborne pathogen has decimated most of the population. You find yourself on a deserted island, lucky to be among the few survivors. But the biological apocalypse is far from being over. It seems that the virus starts to express itself when certain hormonal changes are triggered by highly social behaviors. It has been noted that small groups of survivor seems to be relatively unaffected, but there isn't one single town or clan that wasn't decimated.

## The next step for "no clan" and "lonewolf" servers
This plugin is an attempt at making a self regulated no clan/lonewolf server. The more time you spent with your group, the sicker you'll get. If you're really determined, you might be able to survive the terrible effects of the plague, but large groups of player will find themselves severely handicapped. They will be forced to have much wider bases, spend more time alone to recover and gather up to five times more food and medicine to go raiding. They won't be able to live close to sleepers or protect quarrying operations without getting incredibly sick.

In the end, this is really just an experiment to see if rust can benefit from dynamics that handicap clans and encourage smaller groups.

## Workings
When you get close to a player, you start building up affinity. When you are far away from that player you start loosing affinity, but much slower. This is so you can still collaborate with other players without any penalty. After several minutes of proximity either continuous or briefly interrupted, you become associated with the player with which you have been playing. At that point, you start gaining plague levels. The higher the plague levels, the higher the effects and it takes ten times longer to heal then getting sick. Also, the rapidity of the onset and severity of the symptoms depend on the amount of associates that you have.

The mod has a configurable option for a mini team system named the "kin" system. Basically, you can set a small number of players as "kin". These players will not trigger the plague, no matter how long you stay ayound them. This system can be disabled for lonewolf servers.

It still has to be fine tuned, but the idea is you can still interact with your neighbors, or group up for a multi-crew raid, but you can't live in close proximity. At default levels it takes eight continuous minutes to become associated and fifteen continuous minutes to reach the maximum plague effects. Inversely, it takes 80 minutes to reach zero affinity and 150 minutes to lose all plague effects.

## Plague levels
- 1 Decreased Health Regen
- 2 Increased hunger
- 3 Increased thirst
- 4 No Health Regen
- 5 No comfort
- 6 Highly Increased Hunger
- 7 Highly Increased Thirst
- 8 Cold temperature
- 9 Bleeding
- 10 Radiation effect

## Features
- Automatic associates detection and penalty attributions.
- Radius based proximity detection
- Incremental penalties
- Time based progression and recovery
- Kin system
- Commands
- Configuration files
- Database persistence

## Planned features
- Collaboration analytics
- Sick kin dynamics
- Anti cheesing balances

## Configuration
```js
{
  "affinityDecRate": 30,
  "affinityIncRate": 50,
  "disableSleeperAffinity": false,
  "maxKin": 2,
  "maxKinChanges": 3,
  "plagueDecreaseRate": 60,
  "plagueIncreaseRate": 100,
  "plagueMinAffinity": 6000,
  "plagueRange": 20,
  "UseFriends": false,
  "UseClans": false,
  "UseTeams": false,
  "friendsAutoKin": false
}
```
    - `plagueRange` - This unit is a radius in meters and represents the range of the infection. Every other players that are present inside of this sphere around the player will have their plague level increased and so will the player at the center of the sphere. This value is very import for performance and balance, if you set it too high, people will be able to abuse the plague system more easily by sleeping near people's bases and such. Its also critical for performance, the higher the value, the worse the performance will be.
	- `plagueIncreaseRate` - 
	- Every time the plugin ticks, this value will be added to a player's plague level, but only once the player has first reached the level of associate with another player.
	- `plagueDecreaseRate` - Every time the plugin ticks and the player is alone or with kin, this value will be subtracted from his plague level.
	- `plagueMinAffinity` - The affinity level at which players will start gaining plague levels.
	- `affinityIncRate` - The rate at which affinity between players increase for every plugin tick.
	- `affinityDecRate` - The rate at which affinity between players decreases for every plugin tick.
	- `maxKin` - The maximum amount of kin a player can have
	- `maxKinChanges` - The amount of time a player can change his kin list between server restarts
	- `disableSleeperAffinity` - Setting this value to "true" will prevent players near sleepers from ganing affinity with them.
	- `UseFriends` - Use the Friends plugin for friendsAutoKin
	- `UseClans` - Use any of the available Clans plugins for friendsAutoKin
	- `UseTeams` - Use built-in teams for friendsAutoKin
	- `friendsAutoKin` - On proximity check, automatically check for friends/clans/teams and add them to kin to prevent infection.

## Credits
Thanks to the original dev, Psi|ocybin.
