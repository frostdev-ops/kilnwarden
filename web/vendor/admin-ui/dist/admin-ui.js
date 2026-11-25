import { Navigate as e, Outlet as t, Route as n, Routes as r, useNavigate as i, useParams as a } from "react-router-dom";
import { useMutation as o, useQuery as s, useQueryClient as c } from "@tanstack/react-query";
import { Fragment as l, createContext as u, createElement as d, forwardRef as f, isValidElement as p, useCallback as m, useContext as h, useEffect as g, useId as _, useLayoutEffect as v, useMemo as y, useRef as b, useState as x, useSyncExternalStore as S } from "react";
import { create as C } from "zustand";
import { Fragment as w, jsx as T, jsxs as E } from "react/jsx-runtime";
import { MoreHorizontal as ee, Plus as D, X as te } from "lucide-react";
import { createPortal as O } from "react-dom";
import { useVirtualizer as ne } from "@tanstack/react-virtual";
import k from "leaflet";
var re = {
	language: { name: "English" },
	common: {
		add: "Add",
		back: "Back",
		cancel: "Cancel",
		close: "Close",
		copy: "Copy",
		delete: "Delete",
		dismiss: "Dismiss",
		done: "Done",
		install: "Install",
		ok: "OK",
		open: "Open",
		refresh: "Refresh",
		remove: "Remove",
		reset: "Reset",
		retry: "Retry",
		save: "Save",
		show: "Show",
		undo: "Undo",
		update: "Update",
		confirmWord: {
			clean: "clean",
			delete: "delete",
			forget: "forget",
			restore: "restore",
			switch: "switch",
			uninstall: "uninstall"
		}
	},
	languageSettings: {
		title: "Language",
		launcher: "Launcher",
		system: "System (default)",
		launcherHint: "The launcher's own texts. Applies at once; a text a translation lacks shows in English. Messages from Kiln itself stay in English for now.",
		files: "Your translation files:",
		filesReload: "Read the translation files again",
		"filesRead.one": "{count} translation file read",
		"filesRead.other": "{count} translation files read",
		filesBad: "{0}: {1}",
		game: "Kiln in game",
		gameAuto: "Same as the game (default)",
		gameHint: "Kiln's own menus and messages in the game (kiln.json ui.language). A language Kiln has no texts for shows English. Mods choose their own.",
		gameNoKiln: "Install Kiln on Runtime first.",
		gameNoGame: "No game selected."
	},
	admin: /* @__PURE__ */ JSON.parse("{\"players\":{\"title\":\"Players\",\"sub\":\"Live from the server, every second. Actions run at once.\",\"needsWhat\":\"The player list\",\"nobodyAside\":\"Nobody is in the world. Players show here while they are connected.\",\"pickInWorld\":\"Pick In the world to act on a player.\",\"tabWorld\":\"In the world {count}\",\"tabSeen\":\"Everyone seen {count}\",\"worldTable\":\"Players in the world\",\"knownTable\":\"Known players\",\"colPlayer\":\"Player\",\"colState\":\"State\",\"colHealth\":\"Health\",\"colPosition\":\"Position\",\"colLastSeen\":\"Last seen\",\"colFirstSeen\":\"First seen\",\"entityId\":\"entity {entity}\",\"stateDead\":\"Dead\",\"godMode\":\"God mode\",\"stateAdmin\":\"Admin\",\"statePlaying\":\"Playing\",\"nobody\":\"Nobody is in the world.\",\"unknown\":\"Unknown\",\"online\":\"Online\",\"inPermissions\":\"In permissions\",\"knownEmpty\":\"Players are remembered once they join while the launcher is connected.\",\"killTitle\":\"Kill {name}?\",\"killBody\":\"{name} dies at once and respawns as after any death, leaving a tombstone as the server's death rules say.\",\"kill\":\"Kill\",\"killed\":\"{name} was killed\",\"saveSpot\":\"Save this spot as a waypoint\",\"waypointPlaceholder\":\"Home base\",\"spotName\":\"{name} spot\",\"savedWaypoint\":\"Saved waypoint {name}\",\"rightsNone\":\"none\",\"idLine\":\"{id}, entity {entity}\",\"noSteamId\":\"no SteamID\",\"where\":\"At {pos}. Rights: {rights}.\",\"whereHealth\":\"At {pos}, health {health}. Rights: {rights}.\",\"combat\":\"Combat\",\"healed\":\"Healed {name}\",\"healFull\":\"Heal to full\",\"godOn\":\"God mode on for {name}\",\"godOff\":\"God mode off for {name}\",\"inventory\":\"Inventory\",\"inventoryOf\":\"{name}'s inventory\",\"teleportTo\":\"Teleport to…\",\"tpPlayer\":\"Player: {name}\",\"tpWaypoint\":\"Waypoint: {name}\",\"tpGrave\":\"Grave: {name} ({ago})\",\"tpCoords\":\"Coordinates…\",\"teleported\":\"Teleported {name}\",\"pickTarget\":\"Pick where to teleport to.\",\"unknownTarget\":\"Unknown target.\",\"teleport\":\"Teleport\",\"go\":\"Go\",\"findGround\":\"Find ground\",\"pickItem\":\"Pick an item from the list.\",\"amountRange\":\"Amount: 1 to 10000.\",\"levelRange\":\"Level: 1 to 100.\",\"gave\":\"Gave {given} × {item}\",\"gavePartial\":\"Gave {given} × {item} (only room for {given})\",\"giveItem\":\"Give an item\",\"searchItems\":\"Search the server's items\",\"amount\":\"Amount\",\"level\":\"Level\",\"levelPlaceholder\":\"level\",\"rarity\":\"Rarity\",\"rarities\":{\"common\":\"Common\",\"uncommon\":\"Uncommon\",\"rare\":\"Rare\",\"epic\":\"Epic\",\"legendary\":\"Legendary\"},\"give\":\"Give\",\"levelToCap\":\"Level: 1 to {cap}.\",\"levelToTheCap\":\"Level: 1 to the cap.\",\"isLevel\":\"{name} is level {level}\",\"respecTitle\":\"Respec {name}?\",\"respecBody\":\"Every skill node of {name} is locked again and all spent skill points come back. It is free, like the game's own reset.\",\"respec\":\"Respec\",\"respecDone\":\"Skill points reset for {name}\",\"progression\":\"Progression\",\"levelOf\":\"Level {level} of {cap}\",\"atCap\":\", at the cap\",\"xpOf\":\", {xp:N0} of {needed:N0} XP\",\"xpPending\":\" (+{pending:N0})\",\"pointsUnspent\":\". {points} of {total} skill points unspent.\",\"overBudget\":\" Over budget: the game resets the tree at the next login.\",\"noProgression\":\"No progression data.\",\"reading\":\"Reading…\",\"setLevel\":\"Set level\",\"experience\":\"Experience\",\"gaveXp\":\"Gave {xp} XP for {name}\",\"giveXp\":\"Give XP\",\"skillPoints\":\"Skill points\",\"givePointsTip\":\"Given as the game's skill-point knowledge: {count} more can be given; for more, raise the level\",\"gavePoints\":\"Gave {points} skill point(s) for {name}\",\"givePoints\":\"Give points\",\"respecTree\":\"Respec the skill tree\",\"messageRange\":\"A message of 1 to 500 characters.\",\"told\":\"Told {name}\",\"message\":\"Message\",\"onlySees\":\"Only {name} sees it\",\"send\":\"Send\",\"readingInventory\":\"Reading the inventory…\",\"noInventories\":\"No inventories reported.\",\"slotsUsed\":\"{slots} slots used\",\"levelBadge\":\"Level {level}\",\"equipped\":\"equipped\"},\"connection\":{\"transportAuto\":\"Auto (TCP, then file bridge)\",\"transportTcp\":\"KAP/1 over TCP\",\"transportFile\":\"File bridge\",\"transportSsh\":\"SSH tunnel\",\"connecting\":\"Connecting to the server's Kiln.Admin…\",\"notConnected\":\"Not connected\",\"transport\":\"Transport\",\"reconnect\":\"Reconnect\",\"notAnswering\":\"Kiln.Admin isn't answering.\",\"offline\":\"{reason} The live tools need the server running with Kiln and its Kiln.Admin mod.\"},\"zoneHooks\":{\"build\":\"Building and breaking\",\"terraform\":\"Digging\",\"spawn\":\"Enemy spawns\",\"idle\":\"{label}: not in use yet.\",\"waiting\":\"{label}: waiting: {reason}.\",\"refused\":\"{label}: refused: {reason}.\",\"unknown\":\"{label}: installation status unknown on this server version.\",\"noIntercept\":\"Opening, looting and crafting: Pre interception unavailable.\"},\"zones\":{\"action\":{\"build\":\"Build\",\"break\":\"Dismantle\",\"terraform\":\"Dig & terraform\",\"interact\":\"Open & use\",\"loot\":\"Loot & harvest\",\"craft\":\"Craft\",\"spawn\":\"Enemy spawns\"},\"actionHint\":{\"build\":\"place blocks and props\",\"break\":\"take blocks and props down\",\"terraform\":\"dig, add or flatten terrain\",\"interact\":\"chests, doors, stations, beds, portals, NPCs\",\"loot\":\"loot, harvest, pick items up\",\"craft\":\"craft at stations inside\",\"spawn\":\"enemies spawning inside\"},\"nameRange\":\"Zone name: 1 to 64 characters.\",\"needCentre\":\"Give the centre (x, y and z), or take it from a player or the map.\",\"centreRange\":\"The centre is out of range.\",\"radiusRange\":\"Radius: 1 to 2000 m.\",\"pickDeny\":\"Pick what the zone refuses: {actions}.\",\"ownersMax\":\"At most 64 owners.\",\"centreFromMap\":\"Centre taken from the map\",\"centreFromMapDetail\":\"Name the zone and pick what it refuses.\",\"saved\":\"Saved {name}\",\"added\":\"Added {name}\",\"noOwnersTitle\":\"A zone without owners?\",\"noOwnersBody\":\"Nobody may do the ticked things inside it.\",\"noOwnersAdmins\":\"Nobody but admins may do the ticked things inside it.\",\"addAnyway\":\"Add anyway\",\"title\":\"Protection zones\",\"sub\":\"Only owners may build, dig, open, loot or craft inside. Zones are spheres and take effect at once.\",\"subDenied\":\"Only owners may build, dig, open, loot or craft inside. Zones are spheres and take effect at once. {count} refused so far.\",\"notAvailable\":\"Not available\",\"noGameplay\":\"kiln.gameplay didn't start on this server build.\",\"status\":\"Protection status\",\"tableLabel\":\"Zones\",\"colZone\":\"Zone\",\"inactive\":\"inactive\",\"colRefuses\":\"Refuses\",\"colOwners\":\"Owners\",\"nobody\":\"Nobody\",\"andAdmins\":\", admins\",\"colRadius\":\"Radius\",\"metres\":\"{n} m\",\"colActions\":\"Actions\",\"moreFor\":\"More for {name}\",\"showOnMap\":\"Show on the map\",\"change\":\"Change\",\"removeTitle\":\"Remove {name}?\",\"removeBody\":\"Its protection ends at once.\",\"removed\":\"Removed {name}\",\"empty\":\"No protection zones. Add one here, or right-click the map and pick Protect an area.\",\"changeName\":\"Change {name}\",\"addZoneTitle\":\"Add a zone\",\"formHint\":\"Kiln refuses these in the game's own code. Not every action has been tried in a real game yet.\",\"name\":\"Name\",\"nameHintEdit\":\"Saving keeps the name and replaces the rest.\",\"nameHintAdd\":\"The name of an existing zone replaces it.\",\"namePlaceholder\":\"Main base\",\"radiusLabel\":\"Radius in metres, 1 to 2000\",\"centre\":\"Centre\",\"yHeight\":\"y, height\",\"player\":\"Player\",\"whereStands\":\"Where {name} stands\",\"noPlayerPos\":\"No position for that player.\",\"use\":\"Use\",\"refuseInside\":\"Refuse inside\",\"ownersLabel\":\"Owners, who may do everything inside\",\"noKnownPlayers\":\"No known players yet.\",\"addOwner\":\"Add an owner by SteamID64\",\"adminsToo\":\"Admins may act inside too\",\"saveZone\":\"Save zone\",\"addZone\":\"Add zone\"},\"history\":{\"waypointName\":\"Waypoint names: 1 to 40 characters.\",\"waypointNoPos\":\"No position for that waypoint.\"},\"catalog\":{\"creature\":\"Creature\"},\"pages\":{\"players\":\"Players\",\"map\":\"Map\",\"zones\":\"Zones\",\"items\":\"Items\",\"spawn\":\"Spawn\",\"world\":\"World\",\"combat\":\"Combat\",\"creatures\":\"Creatures\",\"gamedata\":\"Game data\",\"permissions\":\"Permissions\",\"content\":\"Content\",\"saves\":\"Saves\",\"crashes\":\"Crashes\"},\"common\":{\"whatNeedsNewer\":\"{what} needs a newer Kiln.Admin\",\"needsNewer\":\"Needs a newer Kiln.Admin\",\"noOpYet\":\"This server's Kiln.Admin has no {ops} op yet. Update Kiln.Admin on the server (Servers, Mods) and restart it.\",\"playerEntity\":\"Player {entity}\",\"someone\":\"someone\",\"pickPlayer\":\"Pick a player\",\"nobodyOnline\":\"nobody online\",\"searchItems\":\"Search the server's items…\",\"noItemSearch\":\"item_search is not available\",\"gearTag\":\" · gear\",\"enterXyz\":\"Enter x, y and z.\",\"coordsRange\":\"Coordinates out of range.\",\"yHeight\":\"y (height)\",\"steamIdFormat\":\"A SteamID64 has 17 digits (7656119…).\",\"nodeNoMinus\":\"Give the node without '-'; choose Deny instead.\",\"nodeFormat\":\"Permission nodes are dot-separated words of a-z 0-9 _ - : (a final .* matches everything below).\"},\"gameSettings\":{\"minutes\":\"Minutes\",\"playerHealthFactor\":\"Health\",\"playerManaFactor\":\"Mana\",\"playerStaminaFactor\":\"Stamina\",\"playerBodyHeatFactor\":\"Body heat\",\"playerBodyHeatFactorHint\":\"How long you stay warm in the cold\",\"playerDivingTimeFactor\":\"Diving time\",\"shroudTimeFactor\":\"Time in the Shroud\",\"enableDurability\":\"Item durability\",\"enableStarvingDebuff\":\"Starving debuff\",\"foodBuffDurationFactor\":\"Food buff duration\",\"fromHungerToStarving\":\"Hunger to starving\",\"tombstoneMode\":\"On death\",\"tombstoneModeHint\":\"What goes into your tombstone\",\"enableGliderTurbulences\":\"Glider turbulence\",\"weatherFrequency\":\"Weather\",\"randomSpawnerAmount\":\"Random enemy spawns\",\"flameLevelFogRemoval\":\"Fog removal flame level\",\"flameLevelFogRemovalHint\":\"Flame level at which Shroud fog lifts\",\"dayTimeDuration\":\"Day length\",\"nightTimeDuration\":\"Night length\",\"miningDamageFactor\":\"Mining damage\",\"plantGrowthSpeedFactor\":\"Plant growth\",\"resourceDropStackAmountFactor\":\"Resource drops\",\"factoryProductionSpeedFactor\":\"Crafting station speed\",\"perkUpgradeRecyclingFactor\":\"Perk recycling refund\",\"perkCostFactor\":\"Perk cost\",\"experienceCombatFactor\":\"Combat XP\",\"experienceMiningFactor\":\"Mining XP\",\"experienceExplorationQuestsFactor\":\"Exploration & quest XP\",\"aggroPoolAmount\":\"Simultaneous attackers\",\"enemyDamageFactor\":\"Enemy damage\",\"enemyHealthFactor\":\"Enemy health\",\"enemyStaminaFactor\":\"Enemy stamina\",\"enemyPerceptionRangeFactor\":\"Enemy perception\",\"bossDamageFactor\":\"Boss damage\",\"bossHealthFactor\":\"Boss health\",\"threatBonus\":\"Threat bonus\",\"pacifyAllEnemies\":\"Pacify all enemies\",\"pacifyAllEnemiesHint\":\"Enemies only fight back\",\"curseModifier\":\"Curse strength\",\"tamingStartleRepercussion\":\"Startling a tame\",\"fishingDifficulty\":\"Fishing difficulty\",\"group\":{\"player\":\"Player\",\"survival\":\"Survival\",\"world\":\"World\",\"time\":\"Time\",\"rates\":\"Rates\",\"enemies\":\"Enemies\",\"others\":\"Other\"},\"option\":{\"addBackpackMaterials\":\"Add Backpack Materials\",\"everything\":\"Everything\",\"noTombstone\":\"No Tombstone\",\"disabled\":\"Disabled\",\"rare\":\"Rare\",\"normal\":\"Normal\",\"often\":\"Often\",\"extreme\":\"Extreme\",\"automatic\":\"Automatic\",\"easy\":\"Easy\",\"hard\":\"Hard\",\"keepProgress\":\"Keep Progress\",\"loseSomeProgress\":\"Lose Some Progress\",\"loseAllProgress\":\"Lose All Progress\",\"veryEasy\":\"Very Easy\",\"veryHard\":\"Very Hard\",\"fewAmount\":\"Few\",\"manyAmount\":\"Many\"},\"errOnOff\":\"{label}: on or off.\",\"errOption\":\"{label}: unknown option.\",\"errMinutes\":\"{label}: {min} to {max} minutes.\",\"errRange\":\"{label}: {min} to {max}.\"},\"world\":{\"title\":\"World\",\"sub\":\"Time, weather and the world's settings, live on the running server.\",\"timeOfDay\":\"Time of day\",\"broadcast\":\"Broadcast\",\"liveSettings\":\"Live world settings\",\"dawn\":\"Dawn\",\"morning\":\"Morning\",\"noon\":\"Noon\",\"dusk\":\"Dusk\",\"night\":\"Night\",\"midnight\":\"Midnight\",\"timeNow\":\"Time is now {clock}\",\"weatherReset\":\"Weather reset\",\"timeFormat\":\"The time looks like 19:45.\",\"dayNight\":\"Day {day} min · night {night} min\",\"badgeDay\":\"day\",\"badgeNight\":\"night\",\"hour\":\"Hour\",\"clock\":\"Clock\",\"setTime\":\"Set time\",\"resetClock\":\"Reset the game's clock\",\"resetClockHint\":\"Off: the world jumps forward to that time (plants grow, stations run).\",\"resetWeather\":\"Reset weather\",\"resetWeatherHint\":\"Ends the current weather; the game rolls new weather as usual.\",\"sentTo\":{\"one\":\"Sent to {count} player\",\"other\":\"Sent to {count} players\"},\"messageRange\":\"A message of 1 to 500 characters.\",\"broadcastSub\":\"A chat line every player in the world sees\",\"broadcastText\":\"Broadcast text\",\"broadcastPlaceholder\":\"Server restarts in 5 minutes.\",\"sendEveryone\":\"Send to everyone\",\"savedConfig\":\"Saved to the server config\",\"settingNow\":\"{label}: now {value} (live)\",\"minutesValue\":\"{n} min\",\"permanentTitle\":\"Make the live values permanent?\",\"permanentBody\":\"Kiln writes the current values into the server's config (enshrouded_server.json), so they survive restarts.\",\"saveToConfig\":\"Save to config\",\"settingsTitle\":\"World settings\",\"settingsSub\":\"Changes apply at once and last until the server restarts, unless you make them permanent.\",\"reread\":\"Re-read\",\"makePermanent\":\"Make permanent\",\"readOnly\":\"This server build reports the settings as read-only right now (the world may still be loading).\",\"readingSettings\":\"Reading the live settings…\",\"notReported\":\"Not reported by the game\",\"on\":\"On\",\"off\":\"Off\"},\"ui\":{\"confirm\":\"Confirm\",\"healthTitle\":\"{health} / {max} health\",\"online\":\"online\",\"durS\":\"{s}s\",\"durDH\":\"{d}d {h}h\",\"durHM\":\"{h}h {m}m\",\"durM\":\"{m}m\",\"justNow\":\"just now\",\"ago\":\"{dur} ago\"},\"map\":{\"chestGold\":\"Gold chest\",\"chestSilver\":\"Silver chest\",\"chestWood\":\"Wooden chest\",\"chest\":\"Chest\",\"posXZ\":\"X {x} · Z {z}\",\"posXZH\":\"X {x} · Z {z} · H {h}\",\"withLevel\":\"{label} · level {level}\",\"loadingRelief\":\"Loading the game map relief…\",\"loadingReliefPct\":\"Loading the game map relief… {pct}%\",\"reliefUnavailable\":\"Map relief unavailable ({reason}); showing the low-detail map.\",\"noMarkerData\":\"no marker data (game/markers.json, data/markers.json)\",\"finderData\":\"Finder data: {message}\",\"boss\":\"Boss\",\"creatureSpawn\":\"Creature spawn\",\"ore\":\"Ore\",\"drops\":\"Drops: {list}\",\"chestOf\":\"{chest} ({item})\",\"oreVolume\":\"Volume {vol}\",\"oreBuried\":\"Volume {vol} · buried\",\"oreDepth\":\"Volume {vol} · {depth} m below the surface\",\"oreBuriedDepth\":\"Volume {vol} · buried · {depth} m below the surface\",\"healthTip\":\"Health {health} / {max}\",\"waypoint\":\"Waypoint\",\"graveOf\":\"{name}'s grave\",\"graveSomeone\":\"A player's grave\",\"enemy\":\"Enemy\",\"entity\":\"Entity {entity}\",\"noWebgl2\":\"WebGL2 is not available\",\"contextLost\":\"The graphics context was lost; reopen the page.\",\"km\":\"{n} km\",\"metres\":\"{n} m\",\"metreUnit\":\"m\",\"dir\":{\"n\":\"N\",\"ne\":\"NE\",\"e\":\"E\",\"se\":\"SE\",\"s\":\"S\",\"sw\":\"SW\",\"w\":\"W\",\"nw\":\"NW\"},\"countSpawns\":\"{n} spawn points\",\"countGather\":\"{n} locations\",\"countOre\":\"{n} deposits\",\"countChests\":\"{n} chests\",\"tpItemHere\":\"Teleport {who} here\",\"tpItemTo\":\"Teleport {who} to {name}\",\"tpItemGrave\":\"Teleport {who} to this grave\",\"moveHere\":\"Move {who} here.\",\"moveTo\":\"Move {who} to {name}.\",\"moveGrave\":\"Move {who} to this grave.\",\"moveAtHere\":\"Move {who} here (X {x}, Z {z}, height {h}). The server looks for a safe spot near the target.\",\"moveAtTo\":\"Move {who} to {name} (X {x}, Z {z}, height {h}). The server looks for a safe spot near the target.\",\"moveAtGrave\":\"Move {who} to this grave (X {x}, Z {z}, height {h}). The server looks for a safe spot near the target.\",\"teleportedHere\":\"Teleported {who} here\",\"teleportedTo\":\"Teleported {who} to {name}\",\"teleportedGrave\":\"Teleported {who} to this grave\",\"teleportTitle\":\"Teleport {who}?\",\"teleport\":\"Teleport\",\"noHeightRelief\":\"No height known for that spot yet (the map relief is still loading, or there is no relief).\",\"noHeight\":\"No height known for that spot yet.\",\"routeFromHere\":\"Route from {from} to here\",\"routeFromMarked\":\"Route from the marked point to here\",\"measureFromHere\":\"Measure from here\",\"startSet\":\"Start point set\",\"startSetDetail\":\"Click another spot and pick “Route from … to here”.\",\"saveWaypoint\":\"Save waypoint\",\"protectHere\":\"Protect an area here\",\"zoneAddedDetail\":\"Owners and details on the Zones page.\",\"spotSubGround\":\"X {x} · Z {z} · ground ≈{h} m\",\"thisSpot\":\"This spot\",\"saveWaypointHere\":\"Save waypoint here…\",\"protectHereMenu\":\"Protect an area here…\",\"copyCoords\":\"Copy coordinates\",\"coordsCopied\":\"Coordinates copied\",\"unmarkFound\":\"Unmark found\",\"markFound\":\"Mark as found\",\"playerSubHealth\":\"{pos} · health {health}/{max}\",\"stopFollowing\":\"Stop following\",\"followOnMap\":\"Follow on the map\",\"routeFromTo\":\"Route from {from} to …\",\"clickDestination\":\"Now click the destination and pick the route entry.\",\"routeFromToName\":\"Route from {from} to {to}\",\"saveWaypointAt\":\"Save waypoint at {name}…\",\"openPlayers\":\"Open on the Players page\",\"waypointSub\":\"Waypoint · {pos}\",\"deleteWaypoint\":\"Delete waypoint\",\"deleteWaypointTitle\":\"Delete waypoint?\",\"graveWaypoint\":\"{name} grave\",\"graveWaypointNone\":\"Grave grave\",\"zoneSub\":\"{radius} m · refuses {deny}\",\"changeRadius\":\"Change the radius…\",\"radiusOf\":\"Radius of {name}\",\"zoneRadiusSet\":\"{name}: {radius} m\",\"editOnZones\":\"Edit on the Zones page\",\"removeZone\":\"Remove zone\",\"removeZoneTitle\":\"Remove the zone {name}?\",\"mapData\":\"Map data: {message}\",\"noMarkerTexts\":\"No marker texts for {code}.\",\"markerTexts\":\"Marker texts: {message}\",\"secDropped\":\"Dropped by creatures\",\"droppedBy\":\"Dropped by {list} · show all on the map\",\"showingDrops\":\"Showing where {count} creature types that drop {item} spawn.\",\"secWhere\":\"Where to find\",\"chestsHold\":\"{n} chests can hold it\",\"bossTag\":\" · boss\",\"hitOn\":\"{what} · on the map (click to remove)\",\"hitOff\":\"{what} · show on the map\",\"playerSub\":\"Player · {pos}\",\"zoneSearchSub\":\"Protection zone · {radius} m\",\"secLive\":\"Live\",\"secPlaces\":\"Places\",\"placeSub\":\"Place · {pos}\",\"secMarkers\":\"Markers\",\"searchLabel\":\"Search the map\",\"searchPlaceholder\":\"Search items, creatures, places, players…\",\"nothingMatches\":\"Nothing matches.\",\"tabMarkers\":\"Markers\",\"tabFind\":\"Find & filter\",\"tabLive\":\"Live\",\"credit\":\"Map, names and icons: game data © Keen Games. Map and search built on the {map} by Nicolas Ferreira (MIT). Leaflet (BSD-2).\",\"creditMap\":\"Enshrouded Interactive Map\",\"artNeedsClient\":\"Map art needs the Enshrouded client\",\"artPreparing\":\"Preparing map art…\",\"toggleSide\":\"Show or hide the side panel\",\"start\":\"start\",\"point\":\"point\",\"player\":\"player\",\"routeStartGone\":\"Route start ({start}) is not in the world\",\"clearRouteTip\":\"Clear the route\",\"zoomIn\":\"Zoom in\",\"zoomOut\":\"Zoom out\",\"wholeWorld\":\"Whole world\",\"heightReadout\":\"Height {h} m\",\"namePlaceholder\":\"Name\",\"zoneNamePlaceholder\":\"Zone name\",\"ownersMore\":\"Owners…\",\"noMapData\":\"No map data yet: the launcher prepares it from your installed game (Preparing game art in the top bar).\",\"loadingMarkers\":\"Loading the markers…\",\"showAll\":\"Show all\",\"hideAll\":\"Hide all\",\"placeNames\":\"Place names\",\"hideFound\":\"Hide found\",\"chestLevel\":\"Chest level\",\"chestLevelTo\":\"to\",\"toggleGroup\":\"Show or hide the whole group\",\"mapLook\":\"Map look\",\"contourEvery\":\"Contour lines every\",\"shroudZones\":\"Shroud zones\",\"animateShroud\":\"Animate the Shroud\",\"flameLevelTip\":\"Shroud zones above this Flame level are drawn as dangerous (red, hatched)\",\"flameLevel\":\"Flame level\",\"namesInTip\":\"The game's own texts for marker names, categories and place names\",\"namesIn\":\"Names in\",\"markersOn\":\"{shown} of {total} markers on · {found} marked found. Click a marker to mark it found.\",\"sourceGame\":\"Markers from your game's data.\",\"sourceGameBuild\":\"Markers from your game's data (build {build}).\",\"sourceCopy\":\"Markers from a local copy (the game's own are made when the launcher prepares the map art).\",\"finderMissing\":\"The finder's data (creature spawns, plants, ore, chest loot) is not extracted from the game yet; without a local copy of it, the Find tab stays empty.\",\"loading\":\"Loading…\",\"itemDroppedBy\":\"{item}: dropped by\",\"oreGroup\":\"Ore and deposits\",\"surfaceOnly\":\"Surface only\",\"bosses\":\"Bosses\",\"creatures\":\"Creatures\",\"otherGroup\":\"Other\",\"inChests\":\"Found in chests\",\"myFilter\":\"My filter\",\"clear\":\"Clear\",\"filterEmpty\":\"Pick creatures, plants, ore or chest items below (or search an item at the top) to show where they are.\",\"filterLabel\":\"Filter the lists\",\"filterPlaceholder\":\"Filter the lists below…\",\"nOn\":\"{n} on · \",\"moreItems\":\"{n} items in total; type to narrow the list.\",\"liveConnected\":\"Kiln.Admin connected · {n} in the world\",\"liveNotConnected\":\"Live tools not connected.\",\"playerRowPos\":\"{x}, {z} · H {h}\",\"following\":\"Following\",\"follow\":\"Follow\",\"zonesEmpty\":\"Click the map and pick “Protect an area here…”.\",\"waypoints\":\"Waypoints\",\"waypointsEmpty\":\"Click the map and pick “Save waypoint here”.\",\"graves\":\"Graves\",\"aPlayer\":\"A player\",\"noDeaths\":\"No deaths recorded.\",\"livingEnemies\":\"Living enemies\",\"off\":\"off\",\"showEnemies\":\"Show enemies within 80 m of each player (asks the server every 5 s)\",\"route\":\"Route\",\"clearRoute\":\"Clear route\",\"routeEmpty\":\"Click a marker or the map and pick “Route …” to measure the distance from a player or a point.\"},\"crashes\":{\"title\":\"Crash reports\",\"sub\":\"Kiln writes one when a mod or Kiln itself faults: the mod, the exception, the mod list and the log around it.\",\"noOp\":\"This server's Kiln.Admin has no 'crash_reports' op; the list comes from the server folder.\",\"reports\":\"Reports\",\"reportsSub\":\"{n} · newest first\",\"new\":\"new\",\"empty\":\"No crash reports\",\"emptyHint\":\"kiln/crash-reports is empty. That's good.\",\"filterLabel\":\"Filter lines\",\"filterPlaceholder\":\"Filter lines…\",\"openOnServer\":\"Open kiln/crash-reports/{name} on the server to read it (the text needs Kiln.Admin's crash_reports op).\",\"first200\":\"The first 200 KB.\",\"pick\":\"Pick a report\"},\"saves\":{\"matchExact\":\"mod data matches the world file\",\"matchNone\":\"no mod data yet\",\"matchOlder\":\"mod data is older than the world file\",\"matchNewer\":\"mod data is newer than the world file\",\"saved\":\"World saved (revision {revision}, {ms} ms)\",\"backedUp\":\"Backed up: {folder}\",\"revisionDetail\":\"revision {revision}\",\"restoreStaged\":\"Restore of {folder} staged\",\"restoreStagedDetail\":\"It applies at the next server start.\",\"restoreCanceled\":\"Staged restore canceled\",\"nothingStaged\":\"Nothing was staged\",\"nameRule\":\"Backup names: letters, digits, - _ . (up to 64), not starting with a dot.\",\"restoreTitle\":\"Restore this world at the next start?\",\"stageRestore\":\"Stage the restore\",\"restoreBody\":\"Nothing changes now. The {next}, Kiln replaces the world with {folder} (revision {revision}, {time}) before the game opens it.\",\"restoreNext\":\"next time the server starts\",\"restoreLost\":\"Everything built, mined or found since then is lost\",\"restoreLostBody\":\"{lost}. The files it replaces are kept in {path}.\",\"restoreLostPlayers\":{\"one\":\"{lost}, including what the {count} player online now are doing. The files it replaces are kept in {path}.\",\"other\":\"{lost}, including what the {count} players online now are doing. The files it replaces are kept in {path}.\"},\"restoreCancelHint\":\"Until then you can cancel it here. Restart the server to restore now.\",\"restartTitle\":\"Restart the server?\",\"restartOnline\":{\"one\":\"{count} player is online and will be disconnected. \",\"other\":\"{count} players are online and will be disconnected. \"},\"restartBody\":\"It saves, shuts down and starts again.\",\"restartBodyRestore\":\"It saves, shuts down and starts again, restoring {name}.\",\"restart\":\"Restart\",\"restarting\":\"Restarting the server\",\"restartFailed\":\"Couldn't restart from here\",\"title\":\"Saves\",\"sub\":\"The world file together with the mod data that belongs to it (kiln.saves).\",\"saveNowTip\":\"Ask the game to save the world now\",\"saveNow\":\"Save now\",\"what\":\"Kiln saves\",\"stagedTitle\":\"A restore is staged: {name}\",\"cancelRestore\":\"Cancel restore\",\"restartNow\":\"Restart now\",\"stagedBody\":\"Nothing has changed yet. At the next server start Kiln puts that world back before the game opens it; the files it replaces are kept in kiln/backups/_before-restore-….\",\"worldSave\":\"World save\",\"kvWorld\":\"World\",\"worldValue\":\"{world} · revision {revision} · {state}\",\"kvLastSaved\":\"Last saved\",\"lastSavedDetail\":\"({reason}; the game saves every {seconds} s)\",\"kvModData\":\"Mod data\",\"generation\":\"· generation {generation}\",\"kvSession\":\"This session\",\"written\":\"{written} saves written\",\"failed\":\", {failed} failed\",\"backups\":\"Kiln backups\",\"backupsSub\":\"kiln/backups: saves first, then copies the world file and its mod data\",\"backupName\":\"Backup name\",\"backUp\":\"Back up\",\"backupsTable\":\"Backups\",\"colWhen\":\"When\",\"colBackup\":\"Backup\",\"staged\":\"staged\",\"colRevision\":\"Revision\",\"colSize\":\"Size\",\"size\":\"{bytes} · {files} files\",\"sizeData\":\"{bytes} · {files} files · {data} data\",\"restoreAtNext\":\"Restore at next start…\",\"empty\":\"No Kiln backups yet\",\"emptyHint\":\"Back up now, before trying something risky.\"},\"items\":{\"searching\":\"Searching\",\"found\":\"{n} found\",\"foundMore\":\"{n}+ found\",\"what\":\"Item search\",\"pickAside\":\"Pick an item, then give it to a player or drop it into the world.\",\"searchLabel\":\"Search items\",\"colItem\":\"Item\",\"colKind\":\"Kind\",\"gear\":\"gear\",\"stacks\":\"stacks\",\"colId\":\"Id\",\"searchingDot\":\"Searching.\",\"noMatch\":\"No item matches that.\",\"typeTwo\":\"Type two letters or more: wood, sword, shroud.\",\"onlyRoom\":\"Only room for {given}.\",\"dropped\":\"Dropped {item}\",\"entityDetail\":\"Entity {entity}.\",\"pickPlayer\":\"Pick a player.\",\"giveTo\":\"Give to\",\"levelPlaceholder\":\"item's\",\"rarityOwn\":\"Item's own\",\"needsOnline\":\"Needs someone online.\",\"toPlayer\":\"To {name}.\",\"noGiveOp\":\"This Kiln.Admin has no give_item op.\",\"dropTitle\":\"Drop into the world\",\"dropHint\":\"A loot bag on the ground.\",\"where\":\"Where\",\"atPlayer\":\"At {name}\",\"atCoords\":\"At coordinates\",\"amountDrop\":\"Amount to drop\",\"drop\":\"Drop\"},\"gameData\":{\"title\":\"Game data\",\"section\":\"{section} {count}\",\"sectionChanged\":\"{section} {count} ({changed} changed)\",\"sub\":\"Templates, arsenals, sequences, buffs, perks, skills and item data\",\"search\":\"Search\",\"searchSub\":\"By name, 0x id or GUID\",\"sectionLabel\":\"Section\",\"searchPlaceholder\":\"name contains…\",\"entries\":\"Entries\",\"nothingFound\":\"Nothing found\",\"nothingFoundHint\":\"Try a shorter name.\",\"entry\":\"Entry\",\"changed\":\"changed\",\"clone\":\"clone\",\"noEntry\":\"No entry picked\",\"noEntryHint\":\"Search a section and click an entry.\",\"valuesHint\":\"Values are pack JSON: numbers, names, flag lists, objects, {{\\\"mul\\\": 1.5}}. They are Kiln.Admin's: they go back when Kiln.Admin reloads or the server restarts.\",\"setDone\":\"Set: {n} value(s) changed\",\"nothingToReset\":\"Nothing of Kiln.Admin's to reset\",\"path\":\"Path\",\"pathPlaceholder\":\"path (e.g. Health.max)\",\"value\":\"Value\",\"valuePlaceholder\":\"value: 120, {{\\\"mul\\\": 1.5}}\",\"set\":\"Set\",\"fieldInfo\":\"{type}: {value} (game: {original})\",\"fieldInfoRo\":\"{type}: {value} (game: {original}) · read-only\",\"filterFields\":\"Filter fields\",\"filterFieldsPlaceholder\":\"filter fields…\",\"fields\":\"Fields\",\"type\":\"Type\"},\"spawn\":{\"removed\":\"Removed {n}\",\"countRange\":\"Spawn 1 to 20 at a time.\",\"notGuid\":\"That isn't a creature template GUID.\",\"spawned\":\"Spawned {spawned} × {label}\",\"spawnedFailed\":\"Spawned {spawned} × {label}, {failed} failed\",\"title\":\"Spawn\",\"sub\":\"{alive} spawned from here alive, {known} creature types known. Everything spawned here can be removed again.\",\"subLoaded\":\"{alive} spawned from here alive, {known} creature types known, {loaded} enemies loaded. Everything spawned here can be removed again.\",\"what\":\"Spawning\",\"pickCreature\":\"Pick a creature from the list, or paste a template GUID.\",\"formTitle\":\"Spawn a creature\",\"creature\":\"Creature\",\"searchCreatures\":\"Search {n} creatures, or a GUID\",\"pasteGuid\":\"Paste a template GUID\",\"nextTo\":\"Next to {name}\",\"howMany\":\"How many, 1 to 20\",\"spread\":\"Spread, metres\",\"noCatalog\":\"No local creature catalog: the list holds the enemy types the server has seen near players.\",\"alive\":{\"one\":\"{count} spawned from here is alive.\",\"other\":\"{count} spawned from here are alive.\"},\"removeTitle\":\"Remove spawned creatures?\",\"removeBody\":\"Removes every creature spawned through Kiln.Admin since it loaded ({n} alive).\",\"removeN\":\"Remove {n} spawned\",\"removeNone\":\"Remove  spawned\",\"seenTitle\":\"Seen near players\",\"seenSub\":\"Enemy types the server reported. Name them for the picker.\",\"seenTable\":\"Seen enemy types\",\"colName\":\"Name\",\"nameFor\":\"Name for {name}\",\"colTemplate\":\"Template\",\"colSeen\":\"Seen\",\"spawnOne\":\"Spawn 1\",\"seenEmpty\":\"No enemies seen yet. The server learns the types it sees around the spawn point.\",\"notScanned\":\"Not scanned yet.\"},\"combat\":{\"originGame\":\"in combat\",\"originKiln\":\"by an admin action\",\"originOther\":\"fall, drowning or the like\",\"title\":\"Combat\",\"sub\":\"Health, healing, god mode and kills; who died to what.\",\"what\":\"Combat tools\",\"inWorld\":\"In the world\",\"inWorldSub\":\"Health updates with every state push\",\"nobody\":\"Nobody is in the world\",\"nobodyHint\":\"Players show up here while they're connected. Use the entity tools below for NPCs and creatures.\",\"footHint\":\"God mode is held by Kiln.Admin: it ends when Kiln.Admin reloads or the server restarts. Killing leaves the usual tombstone.\",\"healed\":\"Healed {name} (+{n})\",\"noEffect\":\"{name}: no effect\",\"godBadge\":\"god mode\",\"dead\":\"dead\",\"healthUnknown\":\"health unknown\",\"fullHealth\":\"Already at full health\",\"killBody\":\"{name} dies at once and respawns as after any death, leaving a tombstone with their things as the server's death rules say.\",\"killMore\":\"Kill…\",\"hit\":\"Hit: {state}, health {health}\",\"hitKilled\":\"Hit: {state}, health {health} (killed)\",\"entityKilled\":\"Entity {entity} killed\",\"anyEntity\":\"Any entity\",\"anyEntitySub\":\"NPCs, villagers, creatures: by entity id (from Spawn, the map or the creature list)\",\"entityId\":\"Entity id\",\"entityIdPlaceholder\":\"entity id\",\"readHealth\":\"Read health\",\"invulnerable\":\"invulnerable\",\"damage\":\"Damage\",\"killEntityTitle\":\"Kill entity {entity}?\",\"killEntityBody\":\"A killing blow without credit. Players respawn; creatures drop their loot as usual.\",\"radiusRange\":\"Radius: 1 to 500 m.\",\"nearby\":\"Creatures nearby\",\"nearbySub\":\"{n} within {radius} m · {total} loaded on the server\",\"nearbyHint\":\"Scans NetworkEnemy entities around a point\",\"around\":\"Around\",\"aroundPlayer\":\"Around {name}\",\"aroundCoords\":\"Around coordinates\",\"scan\":\"Scan\",\"colEntity\":\"Entity\",\"noCreatures\":\"No creatures there\",\"nobodyKiller\":\"nobody ({origin})\",\"entityLower\":\"entity {entity}\",\"deaths\":\"Deaths\",\"deathsSub\":\"Kept by the launcher from kiln.combat's death events; newest first\",\"playerLower\":\"player {entity}\",\"killedBy\":\"Killed by\",\"noDeathsHint\":\"Kiln reports each player death with who or what dealt the killing blow.\"},\"creatures\":{\"anyKind\":\"Any kind\",\"title\":\"Creatures\",\"sub\":\"{templates} templates, {creatures} changed, {changes} by mods.\",\"creatureCount\":{\"one\":\"{count} creature\",\"other\":\"{count} creatures\"},\"changeCount\":{\"one\":\"{count} change\",\"other\":\"{count} changes\"},\"subNone\":\"Kiln.Creatures: catalog, values, AI and commands.\",\"what\":\"Creature tools\",\"footHint\":\"Values set here are Kiln.Admin's: they go back to the game's (or another mod's) when Kiln.Admin reloads or the server restarts. Spawned creatures stay.\",\"nearby\":\"Nearby\",\"nearbySub\":\"{n} within {radius} m\",\"nearbyHint\":\"Creatures, villagers and animals around a point\",\"nothingHere\":\"Nothing here\",\"nothingHereHint\":\"Try a larger radius or another spot.\",\"fieldSet\":\"{field} set\",\"resetDone\":\"Reset: {n} change(s) undone\",\"actionSent\":\"{action} sent\",\"enterValue\":\"Enter a value.\",\"giveTarget\":\"Give the target entity.\",\"attackTitle\":\"Send {name} to attack {target}?\",\"attackBody\":\"The creature attacks that entity at once.\",\"attack\":\"Attack\",\"inspect\":\"Inspect\",\"inspectSub\":\"Click a creature in the list, or give an entity id\",\"layers\":\"{n} change(s)\",\"levelUpTo\":\"{level} (up to {max})\",\"kvStats\":\"Scale · XP · speed · damage\",\"kvHome\":\"Home\",\"kvTarget\":\"Target\",\"kvFeaturesOff\":\"Features off\",\"kvLoot\":\"Loot\",\"lootSlots\":\"{min}-{max} slot(s)\",\"field\":\"Field\",\"valuePlaceholder\":\"value\",\"command\":\"Command\",\"targetEntity\":\"Target entity\",\"targetPlaceholder\":\"target entity\",\"noneTitle\":\"No creature picked\",\"noneHint\":\"Scan nearby or spawn one from the catalog.\",\"spawned\":\"Spawned {n}: {list}\",\"catalog\":\"Catalog\",\"templates\":\"{n} template(s)\",\"catalogHint\":\"Every creature and NPC template the server knows\",\"searchNames\":\"Search names\",\"count\":\"Count\",\"catalogTable\":\"Creature catalog\",\"faction\":\"Faction\",\"boss\":\"boss\"},\"configs\":{\"empty\":\"(empty)\",\"on\":\"on\",\"off\":\"off\",\"reread\":\"Re-read {id}\",\"badPath\":\"Unexpected characters in the setting's path.\",\"listMax\":\"Lists hold up to 500 values.\",\"textMax\":\"Text up to 2000 characters.\",\"appliesNext\":\"applies when the mod loads next\",\"title\":\"Mod configs\",\"sub\":\"Settings of the Kiln mods on the server, from their config files. Each change is checked by Kiln and applied while the server runs.\",\"tabs\":\"Configs\",\"tabError\":\" error\",\"tabCorrected\":\" corrected\",\"none\":\"No mod configs\",\"noneHint\":\"Mods that register a config show up here.\",\"cardSub\":\"config {id} of mod {mod} · {kind} · {file}\",\"cardSubRead\":\"config {id} of mod {mod} · {kind} · {file} · read {ago}\",\"filterLabel\":\"Filter settings\",\"filterPlaceholder\":\"Filter…\",\"rereadTip\":\"Read the file again (Kiln also does this by itself when it changes)\",\"rereadFile\":\"Re-read file\",\"errorTitle\":\"The file has an error, so Kiln keeps the previous values\",\"errorBody\":\"{error} Fix the file by hand, or set a value here: Kiln then writes the file again in full.\",\"correctedTitle\":\"Kiln corrected the file when it read it\",\"pendingNote\":\"Saved values marked “pending” keep their running value until the mod loads again (reload it on the Mods page).\",\"pick\":\"Pick a config\",\"pickHint\":\"Changes are checked by Kiln (types, ranges, options), written to the file with its comments and applied at once.\"},\"content\":{\"kindItems\":\"Items\",\"kindRecipes\":\"Recipes\",\"kindLoot\":\"Loot\",\"kindDrops\":\"Drop tables\",\"title\":\"Content\",\"sub\":\"The game's items, recipes, loot and drop tables as Kiln sees them: what mods and data packs changed, and single values to try out.\",\"what\":\"Content tools\",\"tempTitle\":\"Changes made here are temporary\",\"tempBody\":\"They belong to Kiln.Admin and come off when it reloads or the server restarts. Some (stack sizes, recipes, item stats) also look different on players' unmodded games. For a lasting change, put it in a mod's {path} data pack.\",\"statRegistries\":\"Registries\",\"ready\":\"ready\",\"loadingState\":\"loading\",\"statLoot\":\"{n} with random loot\",\"statDrops\":\"{n} drop tables loaded\",\"statChanged\":\"Changed fields\",\"statChangedSub\":\"by mods, packs and here\",\"dataPacks\":\"Data packs\",\"statPacks\":\"{errors} errors · {conflicts} conflicts\",\"statPacksGuard\":\"{errors} errors · {conflicts} conflicts · stack guard on\",\"lookUp\":\"Look up\",\"newContent\":\"New content\",\"registry\":\"Registry\",\"searchPlaceholder\":\"Search by name or id (Material_T1_Wood_Logs, 0x2b0cf1d5)…\",\"debugHint\":\"The game's debug names; ids work too.\",\"lookEmpty\":\"Look something up\",\"lookEmptyHint\":\"Search above, then read and change its fields.\",\"readingName\":\"Reading {name}…\",\"untilReload\":\"until Kiln.Admin reloads or the server restarts\",\"changedTip\":\"At least one field differs from the game's value\",\"needs\":\"Needs\",\"makes\":\"Makes\",\"chance\":\"Chance\",\"weight\":\"Weight\",\"filterFieldsPlaceholder\":\"Filter fields…\",\"changeableOnly\":\"Changeable only\",\"changedOnly\":\"Changed only\",\"fieldsCount\":\"{n} of {total} fields\",\"now\":\"Now\",\"gameValue\":\"Game's value\",\"typeReadOnly\":\"{type} · read-only\",\"none\":\"(none)\",\"resetTip\":\"Take Kiln.Admin's change off (changes by other mods stay)\",\"backTo\":\"{name}.{path} back to {value}\",\"noFieldMatches\":\"No field matches\",\"first200\":\"The first 200 of {n}; filter to narrow it down.\",\"needNumber\":\"A number, please\",\"needWhole\":\"A whole number, please\",\"textMax\":\"Text up to 2000 characters\",\"seconds\":\"seconds\",\"setTemporary\":\"{entry}.{field} = {value} (temporary)\",\"fieldRule\":\"Field: a path like maxStackSize or equipment.slot\",\"singleSub\":\"This server's Kiln.Admin can't list fields yet: one field at a time\",\"fieldPlaceholder\":\"field (maxStackSize)\",\"read\":\"Read\",\"nowGame\":\"Now {value}, game's {original}\",\"newValue\":\"New value\",\"newValuePlaceholder\":\"new value\",\"packsApplied\":\"Data packs applied again\",\"packsSub\":\"Every mod's content/*.json\",\"reapplyTip\":\"Read every mod's content/*.json again and re-apply\",\"reapply\":\"Re-apply packs\",\"applied\":\"{n} applied\",\"errors\":\"{n} errors\",\"warnings\":\"{n} warnings\",\"noPacks\":\"No data packs\",\"noPacksHint\":\"No mod ships content/*.json.\",\"skipped\":\"Skipped: {list}\",\"conflicts\":\"Conflicts\",\"conflictsSub\":\"Two owners set one field; the later one wins\",\"conflict\":\"{winnerValue} from {winner} · beats {loserValue} from {loser}\",\"noConflicts\":\"No conflicts\",\"syncTitle\":\"Content sync to Kiln clients\",\"syncServing\":\"Serving state {state}: {fields} fields, {bytes} bytes in {parts} parts.\",\"syncNothing\":\"Nothing to send: no content changes need syncing right now.\",\"defsTitle\":\"New content from mods\",\"defsSub\":\"Items and recipes mods define (kiln.content definitions)\",\"defsTable\":\"Definitions\",\"basedOn\":\"Based on\",\"mod\":\"Mod\",\"noDefs\":\"No mod defines new content\",\"noDefsOp\":\"content_definitions is not available\"},\"permissions\":{\"title\":\"Permissions\",\"sub\":\"Who may use which mod features and commands. Kiln groups hold rules; players get groups through their server user group or one by one.\",\"tabGroups\":\"Groups\",\"tabCheck\":\"Check\",\"tabNodes\":\"Nodes\",\"readFailed\":\"Couldn't read the permissions\",\"everyNodeOf\":\"every node of {mod}\",\"everyNode\":\"every node (not console-only ones)\",\"noRules\":\"no rules\",\"denied\":\"denied\",\"allowed\":\"allowed\",\"removeRule\":\"Remove this rule\",\"node\":\"Node\",\"nodePlaceholder\":\"mymod.command.home or mymod.*\",\"allowOrDeny\":\"Allow or deny\",\"allow\":\"Allow\",\"deny\":\"Deny\",\"addRule\":\"Add rule\",\"groupRule\":\"Group names: 1 to 40 of A-Z a-z 0-9 _ : -\",\"created\":\"Created group {group}\",\"kilnGroups\":\"Kiln groups\",\"newGroup\":\"New group\",\"newGroupPlaceholder\":\"new group name\",\"create\":\"Create\",\"everyPlayerTip\":\"Every player is in it\",\"everyPlayer\":\"every player\",\"countsAdmin\":\"counts as admin\",\"groupAdminOn\":\"{group}: counts as admin\",\"groupAdminOff\":\"{group}: no longer admin\",\"defaultNoRemove\":\"The default group can't be removed\",\"removeGroupTitle\":\"Remove the group {group}?\",\"removeGroupBody\":\"Its rules go, and players and user groups lose their membership.\",\"removedGroup\":\"Removed group {group}\",\"inherits\":\"Inherits\",\"stopInheriting\":\"Stop inheriting\",\"noLongerInherits\":\"{group} no longer inherits {other}\",\"nothing\":\"nothing\",\"inherit\":\"Inherit\",\"nowInherits\":\"{group} now inherits {other}\",\"inheritAdd\":\"+ inherit…\",\"rules\":\"Rules\",\"removedRule\":\"{who}: removed {node}\",\"groupAllows\":\"{group}: allows {node}\",\"groupDenies\":\"{group}: denys {node}\",\"serverUserGroups\":\"Server user groups\",\"serverUserGroupsSub\":\"The password groups of enshrouded_server.json, mapped to Kiln groups\",\"userGroups\":\"User groups\",\"userGroup\":\"User group\",\"noRights\":\"no rights\",\"sameRights\":\"Same rights as {list}: Kiln can't tell them apart\",\"userGroupMapped\":\"User group {name} → {groups}\",\"noKilnGroup\":\"no Kiln group\",\"noUserGroups\":\"No user groups\",\"noUserGroupsHint\":\"The server config has no user groups.\",\"howItWorks\":\"A player's own rules come first, then their groups': their own, the ones their user group maps to, {group}, and what those inherit. The most specific rule wins (exact, then the longest {prefix}, then {star}); a deny wins a tie. No rule: the node's own level decides.\",\"kickBanAdmin\":\"Players whose user group may kick and ban count as admins.\",\"theFile\":\"The file\",\"reread\":\"Re-read permissions.json\",\"rereadNow\":\"Re-read now\",\"fileHint\":\"You can also edit it by hand: Kiln reads it again within a second.\",\"on\":\"on\",\"noPlayers\":\"No players\",\"noPlayersHint\":\"Players appear once they have joined while the launcher watched, or once they have rules.\",\"sidNote\":\"{sid} · note \\\"{note}\\\"\",\"checkNode\":\"Check a node…\",\"adminBadge\":\"admin\",\"ugVia\":\"{group} → {via}\",\"ugNone\":\"{group} (mapped to no Kiln group)\",\"ambiguous\":\"ambiguous: {list}\",\"orSep\":\" or \",\"noneMatches\":\"none matches these rights\",\"lastSeen\":\"Last seen {ago}\",\"takeOut\":\"Take out of this group\",\"left\":\"{who} left {group}\",\"onlyEveryone\":\"only the groups everyone gets\",\"addToGroup\":\"Add to group\",\"joined\":\"{who} joined {group}\",\"addToGroupOption\":\"+ add to group…\",\"ownRules\":\"Own rules\",\"playerAllowed\":\"{who}: allowed {node}\",\"playerDenied\":\"{who}: denyed {node}\",\"ownBeats\":\"A player's own rules beat every group rule.\",\"fromDefault\":\"the node's default: {level}\",\"fromPlayer\":\"the player's own rule\",\"fromGroup\":\"group {group}\",\"pickOrSid\":\"Pick a player or give a SteamID64.\",\"checkTitle\":\"Check a permission\",\"checkSub\":\"As Kiln decides it right now\",\"playerOption\":\"Player…\",\"or\":\"or\",\"nodeCheckPlaceholder\":\"node, e.g. mymod.command.home\",\"allowedCap\":\"Allowed\",\"deniedCap\":\"Denied\",\"forWho\":\"for {who}\",\"decidedBy\":\"Decided by\",\"ruleX\":\"rule {rule}\",\"noRuleMatched\":\"no rule matched\",\"nodeLevel\":\"Node level\",\"registered\":\"Registered nodes\",\"registeredSub\":\"What loaded mods ask for; commands register <mod>.command.<name>\",\"default\":\"Default\",\"what\":\"What\",\"checkMore\":\"Check…\",\"noNodes\":\"No nodes registered\"}}"),
	deck: /* @__PURE__ */ JSON.parse("{\"shared\":{\"addAgain\":\"Add again\",\"adding\":\"Adding\",\"beta\":\"Beta {beta}\",\"change\":\"Change\",\"checking\":\"Checking\",\"closeGameFirst\":\"Close the game first\",\"closeGameFirstDot\":\"Close the game first.\",\"desktopUi\":\"Desktop UI\",\"home\":\"Home\",\"installKiln\":\"Install Kiln\",\"isOn\":\"{name} is on\",\"kilnNotInGame\":\"Kiln is not in the game yet.\",\"kilnNotInstalled\":\"Kiln not installed\",\"modsOn\":{\"one\":\"{count} mod on\",\"other\":\"{count} mods on\"},\"modsOnOff\":{\"one\":\"{count} mod on, {off} off\",\"other\":\"{count} mods on, {off} off\"},\"noGame\":\"No game yet.\",\"off\":\"Off\",\"on\":\"On\",\"places\":\"Places\",\"read\":\"Read\",\"reading\":\"Reading…\",\"starting\":\"Starting\",\"steamSetting\":\"One Steam setting\",\"steamUpdating\":\"Steam is updating the game\",\"testBuild\":\"Test build {beta} of {current}\",\"up\":\"Up\",\"updateTo\":\"Update to {version}\",\"whatFailed\":\"{what} failed\"},\"modeCard\":{\"title\":\"Steam Deck\",\"open\":\"Open Deck mode\",\"startIn\":\"Start in\",\"auto\":\"Auto\",\"deck\":\"Deck mode\",\"desktop\":\"Desktop\",\"forced\":\"This start: {flag}.\",\"found\":\"Steam Deck found ({signals}).\",\"notFound\":\"No Steam Deck found.\"},\"app\":{\"tabs\":{\"home\":\"Home\",\"mods\":\"Mods\",\"addMods\":\"Add mods\",\"catalog\":\"Catalog\",\"kiln\":\"Kiln\",\"editor\":\"Editor\",\"help\":\"Help\",\"settings\":\"Settings\"},\"docTitle\":\"{tab} · Kiln\",\"select\":\"Select\",\"openingEditor\":\"Opening the save editor…\",\"startingPct\":\"Starting · {pct}%\",\"startingLeft\":\"Starting · {left}\",\"stateTime\":\"{state} {time}\",\"tabsAria\":\"Deck tabs\",\"previousTab\":\"Previous tab\",\"nextTab\":\"Next tab\",\"buttonsAria\":\"Buttons\",\"move\":\"Move\",\"menu\":\"Menu\",\"here\":\"here\",\"quitKiln\":\"Quit Kiln\"},\"ui\":{\"hold\":\"Hold\",\"wentWrong\":\"Something went wrong\",\"wentWrongTimes\":\"Something went wrong ({count} times)\",\"titleTimes\":\"{title} ({count} times)\"},\"status\":{\"noStart\":\"No start with Kiln yet\",\"lastVanilla\":\"Last start: vanilla\",\"safeIdle\":\"Safe idle: {reason}\",\"listUnreadable\":\"Mod list unreadable: no mods loaded\",\"lastNoMods\":\"Last start: without mods\",\"bootedFailed\":\"Booted, {count} failed\",\"bootedErrors\":{\"one\":\"Booted, {count} error\",\"other\":\"Booted, {count} errors\"},\"bootedClean\":\"Booted, no errors\",\"bytes\":\"{n} B\",\"kb\":\"{n} KB\",\"mb\":\"{n} MB\",\"justNow\":\"just now\",\"minAgo\":\"{n} min ago\",\"hoursAgo\":\"{n} h ago\",\"daysAgo\":\"{n} days ago\"},\"kinds\":{\"kiln\":\"Kiln mod\",\"native\":\"Native mod\",\"plugin\":\"Plugin\",\"emf\":\"EMF mod\",\"modpack\":\"Modpack\",\"runtime\":\"Kiln runtime\",\"bareDll\":\"DLL mod\",\"unknown\":\"Mods\",\"eml\":\"EML mod\",\"xhl\":\"XHL companion (requires XHL-Core)\",\"shroudforge\":\"ShroudForge mod\",\"set\":\"SET mod\",\"enshwick\":\"Enshwick mod\",\"greeneye\":\"Greeneye mod\",\"shroudtopia\":\"Shroudtopia mod\",\"flame\":\"Flame mod\",\"shroudlink\":\"shroudlink mod\",\"ember\":\"Ember mod (doesn't run yet)\",\"dllmod\":\"Enshrouded ModLoader mod\",\"proxy\":\"Loader\",\"data\":\"Game data\",\"save\":\"Save file\",\"reshade\":\"ReShade preset\",\"cheatTable\":\"Cheat table\",\"asi\":\"ASI plugin\"},\"dialogs\":{\"loaded\":\"{name} is loaded\",\"noneLoaded\":\"No profile is loaded\",\"profile\":\"Profile\",\"profileMods\":{\"one\":\"{count} mod\",\"other\":\"{count} mods\"},\"profileModsFor\":{\"one\":\"{count} mod · for {server}\",\"other\":\"{count} mods · for {server}\"},\"loadedWord\":\"Loaded\",\"load\":\"Load\",\"noProfile\":\"No profile\",\"noProfileSub\":\"The game folder decides: its mods load as they are\",\"closeToSwitch\":\"Close the game to switch.\",\"listNotPrepared\":\"Kiln installed, but your mod list could not be prepared\",\"updateKiln\":\"Update Kiln?\",\"installKiln\":\"Install Kiln?\",\"noRuntime\":\"No Kiln runtime in this launcher.\",\"playOnce\":\"Play once\",\"withoutMods\":\"Without mods\",\"withoutModsSub\":\"Kiln runs, no mods load\",\"play\":\"Play\",\"vanilla\":\"Vanilla\",\"vanillaSub\":\"No Kiln. Use another character.\",\"gameFound\":\"Game found\",\"findGameAria\":\"Find the game folder\",\"findGame\":\"Find Enshrouded\",\"useFolder\":\"Use this folder\",\"noFolders\":\"No folders here.\",\"modpackInstalled\":\"Modpack installed\",\"modpackTitle\":\"Install this modpack?\",\"runsCode\":\"It runs code on your PC\",\"quitTitle\":\"Quit Kiln?\",\"quit\":\"Quit\",\"unsavedNamed\":\"Your changes to {name} in the save editor are not saved.\",\"unsaved\":\"Your changes to the character in the save editor are not saved.\",\"busy\":\"The save editor is writing a save right now. Wait until it is done before you quit.\"},\"dataFolder\":{\"title\":\"Data folder\",\"restarting\":\"Restarting\",\"moved\":\"Data folder moved\",\"restartKiln\":\"Restart Kiln\",\"restart\":\"Restart\",\"moving\":\"Moving the data folder\",\"canceling\":\"Canceling\"},\"home\":{\"looking\":\"Looking\",\"lookingSub\":\"for Enshrouded\",\"findGame\":\"Find game\",\"findGameSub\":\"In your Steam libraries\",\"play\":\"Play\",\"checkingGame\":\"Checking the game\",\"updating\":\"Updating\",\"installing\":\"Installing\",\"intoFolder\":\"Kiln goes into the game folder\",\"shroudforge\":\"ShroudForge\",\"shroudforgeSub\":\"Kiln can take over: switch in the desktop UI\",\"install\":\"Install\",\"waitingKiln\":\"Waiting for Kiln\",\"gameNotFound\":\"Game not found\",\"shroudforgeHere\":\"ShroudForge is in this game\",\"kilnFromBeta\":\"Kiln {version} · from the beta, Play installs {to}\",\"kilnUpdateReady\":\"Kiln {version} · update ready\",\"kilnVersion\":\"Kiln {version}\",\"build\":\"Enshrouded · build {build}\",\"modsLabel\":\"Mods\",\"profile\":\"· Profile: {name}\",\"lastBoot\":\"Last boot\",\"sfModsFailed\":\"mods did not load\",\"sfPassFailed\":\"startup pass failed\",\"sfModSkipped\":{\"one\":\"one mod left out, the others loaded\",\"other\":\"{count} mods left out, the others loaded\"},\"sfWhy\":\"ShroudForge: why, and what to do\",\"pickFolder\":\"Pick folder\",\"updateKiln\":\"Update Kiln\",\"how\":\"How\",\"steamSettingAsi\":\"One Steam setting for Ultimate ASI Loader\",\"notLoading\":\"Kiln is not loading\",\"mouthAria\":\"{word}: {sub}\",\"sfModsTitle\":\"ShroudForge mods did not load\",\"sfPassTitle\":\"ShroudForge's startup pass failed\",\"sfSkippedTitle\":\"ShroudForge left a mod out\"},\"mods\":{\"notTaken\":\"not taken\",\"notTakenTitle\":\"The Library did not take {name}\",\"couldNotAdd\":\"Could not add {name}\",\"switchedOn\":{\"one\":\"Switched {count} mod on\",\"other\":\"Switched {count} mods on\"},\"switchedOff\":{\"one\":\"Switched {count} mod off\",\"other\":\"Switched {count} mods off\"},\"skippedWhy\":\"{count} skipped: {why}.\",\"cantUndo\":\"Can't undo that\",\"undoKept\":\"Undo kept later changes\",\"undoKeptDetail\":{\"one\":\"{count} mod was switched since.\",\"other\":\"{count} mods were switched since.\"},\"unselect\":\"Unselect\",\"selected\":\"{count} selected\",\"cooking\":\"Cooking\",\"cookAgain\":\"Cook again\",\"noProfile\":\"No profile is loaded: the mods in the game folder load as they are.\",\"notReadable\":\"Your mods are not readable: {error}\",\"reading\":\"Reading your mods.\",\"appliesNext\":\"{mods} · applies at the next start\",\"gameFolder\":\"Game folder\",\"showAll\":\"Show all mods\",\"loadKind\":\"Load {kind} mods\",\"foundAria\":\"{name}: in the game folder, {note}\",\"foundAriaSelected\":\"{name}: in the game folder, {note}, selected\",\"turnOff\":\"Turn off\",\"turnOn\":\"Turn on\",\"ariaOn\":\"{name}: on\",\"ariaOff\":\"{name}: off\",\"ariaOnSelected\":\"{name}: on, selected\",\"ariaOffSelected\":\"{name}: off, selected\",\"selectedAria\":\"Selected mods\",\"switchOn\":\"Switch on\",\"switchOff\":\"Switch off\",\"of\":\"{n} of {total}\",\"ofSkipped\":\"{n} of {total} · {skipped} skipped: {why}\",\"removeEllipsis\":\"Remove…\",\"clear\":\"Clear\",\"unselectAll\":\"Unselect all {count}\",\"removeManyTitle\":{\"one\":\"Remove {count} mod?\",\"other\":\"Remove {count} mods?\"},\"removedMany\":{\"one\":\"Removed {count} mod\",\"other\":\"Removed {count} mods\"},\"removedLeft\":\"Removed {removed}, {left} left\",\"nothingRemoved\":\"Nothing removed\",\"leftDetail\":{\"one\":\"{lines} It is out of your list and still in the Library.\",\"other\":\"{lines} They are out of your list and still in the Library.\"},\"removeCount\":\"Remove {count}\",\"checkingUses\":\"Checking what uses them.\",\"checkFailed\":\"Kiln could not check what else uses some of them ({error}).\",\"filesLeave\":\"Their files leave the Library.\",\"toRemoveAria\":\"Mods to remove\",\"listsItToo\":{\"one\":\"· {names} lists it too\",\"other\":\"· {names} list it too\"},\"removeTitle\":\"Remove {name}?\",\"removed\":\"Removed {name}\"},\"catalog\":{\"filterKiln\":\"Kiln mods\",\"filterAll\":\"All Enshrouded\",\"filterInstalled\":\"Installed\",\"sortEndorsed\":\"Most endorsed\",\"sortUpdated\":\"Recently updated\",\"sortName\":\"Name\",\"installed\":\"Installed\",\"asking\":\"Asking Nexus Mods…\",\"signInToFind\":\"Sign in to Nexus Mods to find Kiln mods\",\"say\":\"{filter} · {sort} · {total}\",\"sayLooking\":\"{filter} · {sort} · {total} · looking at {more} more\",\"signIn\":\"Sign in to Nexus Mods\",\"showAll\":\"Show all Enshrouded mods\",\"more\":\"More\",\"nothingInstalled\":\"Nothing installed from Nexus Mods yet.\",\"noModsYet\":\"No mods here yet.\",\"opened\":\"Opened on Nexus Mods\",\"mod\":\"Mod\",\"adding\":\"Adding…\",\"openOnNexus\":\"Open on Nexus Mods\",\"updateReady\":\"Update ready: {version}\",\"installedVersion\":\"Installed {version}\",\"needs\":\"Needs {mods}\",\"needInstalled\":\"{name} (installed)\",\"freeAccounts\":\"Free accounts download on the site.\",\"added\":\"Added {name}\",\"inListOff\":\"{name} is in your list, off\",\"get\":\"Get {name}\",\"typeNative\":\"Native\",\"typeEmf\":\"EMF\",\"typeDll\":\"DLL\",\"typeModpack\":\"Modpack\"},\"help\":{\"addXinput\":\"Add xinput9_1_0\",\"needed\":\"Needed for this game\",\"alreadySet\":\"Already set\",\"notNeeded\":\"Not needed with your Proton\",\"screenAction\":\"Screen's action\",\"scroll\":\"Scroll\",\"asiNeeds\":\"Ultimate ASI Loader and its .asi plugins may not start under Valve’s Proton without xinput9_1_0 set to native, builtin. Add the full option below; GE-Proton loads the loader without it.\",\"ifNotLoading\":\"If Kiln does not load with your Proton build, add this to Enshrouded's launch options. GE-Proton 11-11 and newer, and some recent Valve builds, load it without this setting.\",\"steamStep1\":\"Press the {steam} button, open {library}.\",\"library\":\"Library\",\"steamStep2\":\"Select {game}, press the {gear} (or {glyph}), then {properties}.\",\"gear\":\"gear\",\"properties\":\"Properties\",\"steamStep3\":\"In {general}, select {launchOptions}.\",\"general\":\"General\",\"launchOptions\":\"Launch Options\",\"steamStep4\":\"Type the line above. {keys} opens the keyboard.\",\"steamStep5\":\"Back out. The next Play uses it.\",\"neverChanges\":\"Kiln never changes Steam's settings itself.\",\"notLoadingTitle\":\"Kiln not loading?\",\"homeSaysNoKiln\":\"Home says {noKiln} when the game runs but Kiln didn't load into it. Close the game, check the launch option above, and play again.\",\"noKiln\":\"No Kiln\",\"sendUs\":\"Still no Kiln? Send us, from the game folder: {proxyLog} (no such file: Steam never loaded Kiln) and {kilnLog}, and the launcher's log ({launcherLogs}), with your Proton version and Game or Desktop Mode.\",\"gameModeTitle\":\"Kiln in Game Mode\",\"gameModeStep1\":\"Press {steam}, {power}, {toDesktop}.\",\"power\":\"Power\",\"switchToDesktop\":\"Switch to Desktop\",\"gameModeStep2\":\"Open Steam. In {games}, choose {addNonSteam}.\",\"games\":\"Games\",\"addNonSteam\":\"Add a Non-Steam Game to My Library\",\"gameModeStep3\":\"{browse} to the Kiln Launcher AppImage, tick it, {addSelected}.\",\"browse\":\"Browse\",\"addSelected\":\"Add Selected Programs\",\"gameModeStep4\":\"Back in Game Mode it is in {library}, {nonSteam}. Deck mode opens by itself.\",\"nonSteam\":\"Non-Steam\",\"layout\":\"Controller layout: Steam's default {gamepad} template. Optional launch option: {flag}.\",\"gamepad\":\"Gamepad\",\"controls\":\"Controls\",\"touch\":\"The touchscreen works everywhere. Keyboard: arrows, Enter, Escape, Q and E for tabs.\"},\"addMods\":{\"fileMods\":\"{file} ({count} mods)\",\"noGame\":\"No game yet\",\"installFirst\":\"Install Kiln first\",\"notAdded\":\"Not added\",\"addedOn\":\"Added and switched on\",\"alreadyOn\":\"Already on\",\"addedOff\":\"Added, switched off\",\"nothingAdded\":\"Nothing was added\",\"addedOne\":\"Added {name}\",\"addedMany\":\"Added {count} mods\",\"updatedOne\":\"Updated {name}: {from} → {to}\",\"updatedName\":\"Updated {name}\",\"updatedMany\":\"Updated {count} mods\",\"addedAndUpdated\":\"{added} added, {updated} updated\",\"needs\":\"Needs {ids}\",\"alreadyOne\":\"{name} already on\",\"alreadyMany\":\"{count} mods already on\",\"noProfileLoaded\":\"No profile is loaded in this game: load one in Settings\",\"nothingToAdd\":\"Nothing to add\",\"modsHere\":{\"one\":\"{count} mod here\",\"other\":\"{count} mods here\"},\"noModsHere\":\"No mods here\",\"listAria\":\"Mods and folders\"},\"kiln\":{\"readingFolder\":\"Reading the game folder.\",\"inGame\":\"Kiln {version} is in the game.\",\"shroudforge\":\"ShroudForge is in this game. Switch in the desktop UI to install Kiln.\",\"whatDone\":\"{what} done\",\"factsAria\":\"Kiln in the game\",\"version\":\"Version\",\"none\":\"None\",\"updated\":\"Updated\",\"installed\":\"Installed\",\"files\":\"Files\",\"filesChanged\":\"{files}, {changed} changed\",\"runsOn\":\"Runs on\",\"uninstall\":\"Uninstall\",\"removed\":\"Kiln removed\",\"holdUninstall\":\"Hold to uninstall\",\"upToDate\":\"Up to date.\"},\"settings\":{\"auto\":\"Auto\",\"deckMode\":\"Deck mode\",\"desktop\":\"Desktop\",\"glyphDeck\":\"Steam Deck\",\"glyphXbox\":\"Xbox\",\"notSignedIn\":\"Not signed in\",\"updateFailed\":\"Update failed\",\"ready\":\"{version} ready\",\"downloading\":\"Downloading\",\"available\":\"{version} available\",\"checkFailed\":\"Check failed\",\"upToDate\":\"Up to date\",\"desktopSub\":\"Mouse and keyboard, every page\",\"switch\":\"Switch\",\"startIn\":\"Start in\",\"deckOnDevice\":\"Deck mode on this device\",\"desktopOnDevice\":\"Desktop on this device\",\"fire\":\"Animated fire\",\"fireSoftware\":\"Off: the launcher draws without the GPU\",\"fireNoWebgl\":\"Off for this start: KILN_LAUNCHER_NO_WEBGL\",\"glyphs\":\"Button glyphs\",\"fullscreen\":\"Fullscreen\",\"updates\":\"Launcher updates\",\"check\":\"Check\",\"launcherVersion\":\"Kiln Launcher {version}\",\"channel\":\"Channel\",\"switchBack\":\"Switch back\",\"newVersions\":\"New versions\",\"testBuilds\":\"Test builds\",\"testBuildsSub\":\"A launcher from the download page can switch to them\",\"downloadPage\":\"Download page\",\"signOut\":\"Sign out\",\"signIn\":\"Sign in\",\"signInUnavailable\":\"Sign-in is not available in this build.\",\"signedIn\":\"Signed in\",\"profileSub\":\"A saved set of mods and their switches; Mods shows the loaded one\",\"otherFolder\":\"Other folder\",\"notesSwitch\":\"Notes about mod folders\",\"notesSwitchSub\":\"Each note shows once when it is new. Off: only a folder that may still run a mod you switched off\",\"modNotes\":\"Mod notes\",\"modNotesSub\":\"What Kiln last said about your mod folders\",\"footVersions\":\"Launcher {launcher} · Kiln {kiln}\",\"noDeckFound\":\"no Steam Deck found\",\"notesTitle\":\"Notes about your mod folders\",\"restartNow\":\"Restart now\",\"later\":\"Later\",\"openDownloadPage\":\"Open the download page\",\"switchBackRestore\":\"Switch back, put back my setup\",\"readyTitle\":\"Ready: {version}\",\"restoreGoes\":\"Your profiles and settings from before the beta go back before the restart.\",\"settingsStay\":\"Your current profiles and settings stay.\",\"noCopy\":\"No complete copy from before the beta: {warnings}\",\"downloadingDots\":\"Downloading…\",\"upscaler\":\"Upscaler behind the game's FSR 3\",\"upscalerNow\":\"Upscaler now\"}}"),
	mods: {
		engines: {
			unreadable: "Could not read {path}: {reason}.",
			truncated: "A folder in the game folder has more entries than Kiln lists.",
			pluginsTitle: "Engine plugins",
			pluginsSub: "Each one's state file from the last start.",
			folderTitle: "In the game folder",
			folderSub: "Other loaders next to the game.",
			chainHint: "The DLLs chain.ini loads, and which load after Kiln and ShroudForge: {link}.",
			facts: "Kiln runs other loaders' mods with its engine plugins; XHL companions run only through XHL-Core, installed separately; Ember mods are kept but do not run yet."
		},
		tabs: {
			native: "Native and DLLs",
			order: "Load order",
			engines: "Engines",
			configs: "Configs"
		},
		order: {
			hintXhl: "XHL-Core, installed separately, loads these; Kiln never runs their DLLs.",
			hintEmber: "Kept, but they do not run yet: Ember has published no mod ABI.",
			dllmainTitle: "In the proxy, before Kiln",
			dllmainHint: "Native mods of the dllmain phase, with the DLLs chain.ini loads.",
			earlyTitle: "As Kiln starts",
			earlyHint: "Native mods of the early phase. They run in safe idle too.",
			pluginsTitle: "Kiln's plugins",
			pluginsHint: "Kiln plugins, and the plugins that run other loaders' mods; each starts its own mods.",
			bootTitle: "After the plugins",
			bootHint: "Native mods of the boot phase.",
			menuTitleClient: "At the main menu",
			menuTitleServer: "When the server world starts",
			menuHint: "Kiln mods, in the order Kiln resolves from what each needs and comes after.",
			worldTitleClient: "With your world",
			worldTitleServer: "At the first world",
			worldHintClient: "When you play alone or friends join you: Kiln mods for the server side, and native mods of the world phase.",
			worldHintServer: "Native mods of the world phase.",
			folderTitle: "From the game folder",
			folderHint: "The game, or the mod's own loader, loads these: packages Kiln places there when it activates the profile, and mods put there by hand.",
			placedByKiln: "Placed by Kiln",
			gameFolder: "Game folder",
			asideHintCopy: "Mods of every kind, as a start brings them up. Native mods keep their order inside a phase: drag them on Native and DLLs. Kiln copies its own mods into the game's kiln/profiles folder.",
			asideHintLibrary: "Mods of every kind, as a start brings them up. Native mods keep their order inside a phase: drag them on Native and DLLs. Kiln mods load from Kiln's Library, so they are not copied into the game folder; other loaders' mods are placed where their loader reads them.",
			orderInPhase: "Order in its phase",
			showInList: "Show in the list",
			loadsNum: "loads {num}.",
			onClient: "Load order on a client",
			onServer: "Load order on a server",
			kindMods: "{kind} mods",
			kilnPlugins: "Kiln plugins",
			byPlugin: "{group}, by {plugin}",
			withHint: "{group}. {hint}",
			notLoadingTitle: "On, but not loading",
			notLoadingHint: "Each says why.",
			offTitle: "Off",
			offHint: "Light one and it takes its place above.",
			noMods: "No mods yet.",
			loadOrderFor: "Load order for",
			client: "Client",
			server: "Server",
			loadsClient: {
				one: "{count} mod load on a client.",
				other: "{count} mods load on a client."
			},
			loadsServer: {
				one: "{count} mod load on a server.",
				other: "{count} mods load on a server."
			},
			notLoadingCount: "{count} on but not loading."
		},
		list: {
			inGameFolder: "In the game folder",
			moreFor: "More for {name}",
			switchOff: "Switch off",
			switchOn: "Switch on",
			windowsDll: "Windows DLL",
			replaceDllTitle: "Replace DLL",
			replacedDll: "Replaced DLL of {name}",
			confirmReplacement: "Confirm replacement?",
			replaceDllFailed: "Could not replace DLL of {name}",
			openDllFailed: "Could not open DLL",
			loadProfileFirst: "Load a profile in Settings to switch mods",
			bulkOnFiltered: {
				one: "Switches on {count} mod of the {total} the filter shows.",
				other: "Switches on {count} mods of the {total} the filter shows."
			},
			bulkOnAll: {
				one: "Switches on {count} mod of the {total} in the list.",
				other: "Switches on {count} mods of the {total} in the list."
			},
			bulkOffFiltered: {
				one: "Switches off {count} mod of the {total} the filter shows.",
				other: "Switches off {count} mods of the {total} the filter shows."
			},
			bulkOffAll: {
				one: "Switches off {count} mod of the {total} in the list.",
				other: "Switches off {count} mods of the {total} in the list."
			},
			everyOnFiltered: "Every mod the filter shows is on already.",
			everyOnAll: "Every mod in the list is on already.",
			everyOffFiltered: "Every mod the filter shows is off already.",
			everyOffAll: "Every mod in the list is off already.",
			cantSwitch: "{count} can't be switched: {why}.",
			leftOut: "{count} left out: {why}.",
			switchedOn: {
				one: "Switched {count} mod on",
				other: "Switched {count} mods on"
			},
			switchedOff: {
				one: "Switched {count} mod off",
				other: "Switched {count} mods off"
			},
			listNotOpen: "the mods list is not open",
			cantUndo: "Can't undo that",
			cantUndoDetail: "{why}. Switch the mods in the list instead.",
			undoKept: "Undo kept later changes",
			undoKeptDetail: {
				one: "{count} mod was switched since.",
				other: "{count} mods were switched since."
			},
			skipped: "{count} skipped: {why}.",
			selOn: "Switches on {count} of the {total} selected.",
			selOff: "Switches off {count} of the {total} selected.",
			selEveryOn: "Every selected mod that has a switch is on already.",
			selEveryOff: "Every selected mod that has a switch is off already.",
			closeGameFirst: "Close the game first",
			selRemove: "Removes {count} of the {total} selected.",
			addMods: "Add mods",
			openFilesFailed: "Could not open files",
			libraryRefused: "The Library did not take {name}",
			isOn: "{name} is on",
			writtenToGame: "Written to the game.",
			addFailed: "Could not add {name}",
			loadDropins: "Load the game-folder {kind} mods",
			addToMyMods: "Add to my mods",
			openFolder: "Open folder",
			replaceDllMenu: "Replace DLL…",
			settings: "Settings",
			configs: "Configs",
			cookAgain: "Cook again",
			useVersion: "Use {version}",
			rollBack: "Roll back to {version}",
			rollBackEarlier: "Roll back to {version} (earlier build)",
			updates: "Updates…",
			removeVersion: "Remove {version}",
			filter: "Filter",
			kind: "Kind",
			everyKind: "Every kind",
			shownOn: "Shown on ({count})",
			allOn: "All on ({count})",
			shownOff: "Shown off ({count})",
			allOff: "All off ({count})",
			checkUpdatesHint: "Looks for newer versions of your mods. A notice and a link: nothing is downloaded or installed.",
			checking: "Checking…",
			checkUpdates: "Check for updates",
			catalog: "Catalog",
			addModsHint: "A zip, 7z, rar or DLL of any kind (a folder: drop it on the window)",
			needsDesktop: "Needs the desktop app: drop a file instead",
			replaceDllConfirm: "Replace DLL?",
			selectedMods: "Selected mods",
			selectedCount: "{count} selected",
			removeMenu: "Remove…",
			clear: "Clear",
			notReadable: "Your mods are not readable.",
			notReadableHint: "Nothing was changed: Play still starts the game with the mods that were on. The reason is above.",
			firstMod: "Add your first mod",
			firstModHint: "Drop its {files} anywhere on this window: Kiln, native, EMF, EML, ShroudForge, SET and the other loaders' mods all go here. It is on at the next Play.",
			firstModHintCatalog: "Drop its {files} anywhere on this window: Kiln, native, EMF, EML, ShroudForge, SET and the other loaders' mods all go here. Or find one in the Catalog. It is on at the next Play.",
			mods: "Mods",
			select: "Select {name}",
			selectHint: "Select (Shift+click: a range, Ctrl+A: every mod shown, Esc: none)",
			nameWithId: "{name} ({id})",
			useChangedOf: "{action} of {name}",
			useChangedHint: "Kiln takes the files in its folder over as the mod's files, with the name and info they give now",
			takingOver: "Taking over…",
			addNamed: "Add {name} to my mods",
			loadDropinsHint: "Load every {kind} mod in the game folder",
			addHint: "Copy it into the library and switch it on",
			adding: "Adding…",
			loadThem: "Load them",
			addIt: "Add it",
			versionsInLibrary: "{count} versions in the library",
			noMatch: "No mod matches that.",
			removeCount: "Remove {count}",
			removeManyTitle: {
				one: "Remove {count} mod?",
				other: "Remove {count} mods?"
			},
			checkingUsers: "Checking what uses them.",
			checkUsersFailed: "Kiln could not check what else uses some of them ({error}). Remove stops for a mod something uses.",
			filesLeave: "Their files leave the Library.",
			modsToRemove: "Mods to remove",
			filesGone: "its files are gone already; it leaves your list",
			alsoListed: {
				one: "{names} lists it too and will show it as missing",
				other: "{names} list it too and will show it as missing"
			},
			removedMany: {
				one: "Removed {count} mod",
				other: "Removed {count} mods"
			},
			removedSome: "Removed {removed}, {left} left",
			nothingRemoved: "Nothing removed",
			leftInLibrary: {
				one: "{list} It is out of your list and still in the Library.",
				other: "{list} They are out of your list and still in the Library."
			},
			leftItem: "{name}: {why}",
			removeTitle: "Remove {name}?",
			removeVersionTitle: "Remove {name} {version}?",
			removedVersion: "Removed {name} {version}",
			removed: "Removed {name}"
		},
		allMods: "All mods",
		foldersAndNotes: "Folders and notes"
	},
	profiles: {
		gameFolder: { loaderLine: "{name}'s own loader ({file}) is in the game folder and loads its mods itself." },
		native: {
			dllmain: "Dllmain",
			dllmainHint: "In the proxy, before anything else. Can hang the start.",
			early: "Early",
			earlyHint: "At the start of Kiln's boot. Runs in safe idle too.",
			boot: "Boot",
			bootHint: "After Kiln's services and plugins.",
			world: "World",
			worldHint: "At the first world. Needs hooks.",
			dllmainBanner: "These load inside the proxy. A bad one can stop the game from starting.",
			title: "Native mods",
			sub: "Drag to set the order inside a phase.",
			empty: "No native mods on your list. Drop a DLL, or a folder with native.json, anywhere on the window.",
			phaseLabel: "{phase} phase",
			pluginsTitle: "Kiln plugins",
			pluginsSub: "DLLs with kiln_plugin_main. They load after Kiln's services.",
			noPlugins: "No plugins on your list.",
			facts: "Native mods load by phase, in the order set here. Kiln still applies their after and before rules."
		},
		modList: {
			lit: "Lit",
			mod: "Mod",
			version: "Version",
			notInLibrary: "Not in the library",
			missing: "Missing",
			actions: "Actions",
			none: "None on your list."
		},
		chain: {
			orderSentence: "{targets} loads after Kiln and ShroudForge so that {early} loads first (compat rule {id}).",
			loadsFirstRule: "Loads first: rule {id}",
			loadsLateRule: "Loads late: rule {id}",
			sides: "{early} before {targets}",
			hookedFirst: "Hooked first",
			hookedFirstHint: "This module hooked the function before Kiln; Kiln chained over it.",
			hookedOver: "Hooked over",
			hookedOverHint: "This module hooked over Kiln's hook after the start.",
			refused: "Refused",
			refusedHint: "A hosted engine refused or failed its hook here.",
			overlap: "Overlap",
			overlapHint: "Both hook the same function.",
			actionWarn: "Warns",
			actionLate: "Loads late",
			actionDisable: "Switches off",
			actionBefore: "Sets the load order",
			gated: "Loads first: shroudlink's client addon has to find the game's functions before Kiln hooks them, or its MODS tab switches off.",
			gatedLate: "Loaded late, shroudlink's client addon switches its MODS tab off. Switch late off so it loads first again.",
			ariaLabel: "Closed DLL mods",
			title: "Load order with other DLLs",
			sub: "DLLs chain.ini loads. Late ones load after Kiln and ShroudForge, so their hooks chain after ours.",
			notListed: "This profile's DLLs are not listed",
			changed: "Changed since the game's files were written: it applies at the next Activate or Play.",
			orderNotChanged: "Load order not changed",
			withRule: "{text} (compat rule {id})",
			empty: "No proxy DLLs in the chain and no dllmain natives in this profile.",
			entries: "Chain entries",
			gameHint: "kiln/chain: a proxy the installer moved aside (every profile)",
			profileHint: "A native mod of this profile",
			game: "Game",
			profile: "Profile",
			loaderHint: "EMF's loader starts EMF itself. Kiln's own EMF support checks for it when Kiln starts, so the loader cannot load after Kiln.",
			alwaysFirst: "Always loads first",
			offByRule: "Off by rule {id}",
			lateLabel: "Load after Kiln and ShroudForge",
			rule: "Rule {id}",
			everyProfile: "Every profile of this game",
			overlapsTitle: "Overlaps last session",
			noHookMap: "No hook map yet. Start the game once with this Kiln.",
			noOverlaps: "No other DLL hooked a function Kiln hooks.",
			overlapsLabel: "Overlaps",
			overlapWhoLib: "{module} ({library}) and {owners}",
			overlapWho: "{module} and {owners}",
			loadLate: "Load late",
			rulesTitle: "Compat rules",
			ruleFileBad: "A rule file did not read",
			noRule: "No rule applies to this profile.",
			noRuleKnown: "No rule applies to this profile ({count} known).",
			activeRules: "Active rules",
			yourRule: "Your rule",
			kilnRule: "Kiln's rule",
			moreIdle: {
				one: "{count} more rule does not apply.",
				other: "{count} more rules do not apply."
			}
		},
		layout: {
			applied: "Applied",
			loaded: "Loaded",
			steamToo: "Steam's Play button uses it too.",
			apply: "Apply",
			activate: "Activate",
			orderSub: "Every mod that is on, of every kind, in the order a start brings them up.",
			nativeSub: "Native mods by load phase, the DLLs chain.ini loads, and Kiln plugins.",
			enginesSub: "Which loader runs which kind of mod in this game, and what each one said at the last start.",
			configsSub: "The config files of your mods.",
			noGame: "No game yet",
			noGameHint: "Find Enshrouded on Play first.",
			play: "Play",
			noProfile: "No profile is loaded",
			noProfileHint: "Load one in Settings, Profiles: these views show the mods that are on.",
			settings: "Settings",
			notReadable: "Your mod list is not readable",
			loadedProfile: "The loaded profile (Settings, Profiles)",
			changedSince: "Changed since written",
			forServer: "For {name}",
			changed: "Changed",
			changedText: "Your mods changed since they were written to the game. Apply writes them now; Play does too."
		},
		dropins: {
			kiln: "Kiln mods",
			native: "Native mods",
			plugin: "Plugins",
			emf: "EMF mods",
			eml: "EML mods",
			shroudforge: "ShroudForge mods",
			shroudforgeFolder: "ShroudForge's mods folder (mods)",
			greeneye: "Greeneye mods",
			flame: "Flame mods",
			shroudtopia: "Shroudtopia mods",
			enshwick: "Enshwick mods",
			enshwickHint: "Enshwick's loader runs every folder in mods once this profile has an Enshwick mod.",
			shroudlink: "shroudlink mods (servers)"
		},
		settings: {
			title: "{name}: folders and notes",
			name: "Name",
			modData: "Mod data",
			modDataHint: "Per-mod data folders. World data is always shared.",
			shared: "Shared",
			thisProfile: "This profile",
			dropins: "Drop-in folders",
			dropinsHint: "Also load mods placed in the game folder by hand.",
			engines: "Other engines' folders",
			enginesHint: "Where other loaders' mod pages say to extract their mods.",
			alwaysLoad: "SET mods ({set}) and Enshwick mods ({enshwick}) always load. Ember mods are found but do not run yet.",
			notes: "Notes"
		},
		facts: {
			on: "On",
			total: {
				one: "{count} mod.",
				other: "{count} mods."
			},
			totalList: {
				one: "{count} mod: {list}.",
				other: "{count} mods: {list}."
			},
			kindCount: "{n} {kind}"
		}
	},
	servers: /* @__PURE__ */ JSON.parse("{\"settings\":{\"range\":\"{min} to {max}\",\"saved\":\"Settings saved\",\"notSaved\":\"Not saved\",\"running\":\"The server is running\",\"runningText\":\"The game rewrites this file while it runs: stop it to change settings.\",\"discard\":\"Discard\",\"general\":\"General\",\"serverName\":\"Server name\",\"playerSlots\":\"Player slots\",\"queryPort\":\"Query port (UDP)\",\"preset\":\"Difficulty preset\",\"voiceMode\":\"Voice chat mode\",\"proximity\":\"Proximity\",\"global\":\"Global\",\"chat\":\"Chat\",\"voice\":\"Voice\",\"text\":\"Text\",\"playerSub\":\"Changing a value sets the preset to Custom\",\"groups\":\"User groups\",\"groupsSub\":\"The server picks a player's group from the password they type; every group needs its own.\",\"addGroup\":\"Add group\",\"name\":\"Name\",\"password\":\"Password\",\"reserved\":\"Reserved slots\",\"removeGroup\":\"Remove group\",\"banned\":\"Banned accounts\",\"bannedSub\":\"SteamID64, one per line\"},\"backups\":{\"made\":\"Backup made\",\"staged\":\"Staged: restart the server to restore it\",\"restored\":\"Restored {name}\",\"backup\":\"Backup\",\"kind\":\"Kind\",\"tarball\":\"tarball\",\"madeCol\":\"Made\",\"size\":\"Size\",\"stopToRestore\":\"Stop the server to restore a tarball\",\"restore\":\"Restore\",\"title\":\"Backups\",\"subRunning\":\"A backup now is made right after a fresh save.\",\"subStopped\":\"The server is stopped: a backup is a tarball of the save.\",\"now\":\"Back up now\",\"empty\":\"No backups yet\",\"restoreTitle\":\"Restore backup\",\"stageConfirm\":\"Stage {name} for the next start? Restart the server to apply it.\",\"replaceConfirm\":\"Replace the world with {name}? The current save moves to the backup folder first.\"},\"configs\":{\"title\":\"Configs\",\"sub\":\"The server is stopped: changes go to the files and apply at the next start.\"},\"runner\":{\"kilnctl\":\"kilnctl (Wine)\",\"steam\":\"Steam\",\"windows\":\"Windows\",\"remote\":\"Remote (SSH)\"},\"overview\":{\"fileBridge\":\"File bridge\",\"sshTunnel\":\"SSH tunnel\",\"openWebPanel\":\"Open web panel\",\"webPanelError\":\"Could not open the web panel\",\"noKiln\":\"Kiln isn't installed\",\"stopToInstall\":\"Stop the server to install it.\",\"facts\":\"Server facts\",\"steps\":\"Steps/s\",\"maxStep\":\"Max step\",\"ms\":\"{ms} ms\",\"runBy\":\"Run by\",\"port\":\"Port\",\"admin\":\"Admin\",\"noEndpoint\":\"No endpoint\",\"off\":\"Off\",\"policy\":\"Policy\",\"game\":\"Game\",\"process\":\"Process\",\"stages\":\"Start stages\",\"stage\":{\"stopping\":\"Stopping\",\"preparing\":\"Preparing\",\"wine\":\"Wine\",\"process\":\"Server process\",\"world\":\"Loading the world\",\"running\":\"Running\"},\"stageHint\":{\"stopping\":\"The world is saved first\",\"remote\":\"kilnctl starts it over SSH\",\"preparing\":\"Checks the folder and Kiln's setup\",\"wine\":\"The runner and its prefix\",\"process\":\"enshrouded_server.exe starts\",\"world\":\"The game loads the world\",\"running\":\"Players can join\"},\"elapsed\":\"Elapsed\",\"left\":\"About {left} left. The last start took {last}.\",\"longer\":\"Taking longer than the last start ({last}). The console shows what it is doing.\",\"stoppingFirst\":\"Stopping first\",\"stoppingHint\":\"The restart saves the world and stops the server, then starts it again.\",\"srStage\":\"{name} is starting: {stage}.\",\"srStopping\":\"{name} is restarting: it stops first.\",\"stageState\":\"({state})\",\"noLast\":\"The first start the launcher times; players can join when it says Running.\",\"whileStarts\":\"While it starts\",\"can\":{\"watch\":\"Watch every line\",\"watchHint\":\"The console follows the start as it happens.\",\"leave\":\"Leave this page\",\"leaveHint\":\"The start keeps going; the server's dot shows it, and a toast says when it runs.\",\"stop\":\"Stop\",\"stopHint\":\"Ends the start and shuts the server down.\",\"join\":\"Players join at Running\",\"joinHint\":\"The world is still loading.\"},\"kilnUpdate\":\"Kiln update\",\"kilnUpdateRunning\":\"{latest} is here; this server runs {current}. Update stops it, installs and starts it again; players online are disconnected meanwhile.\",\"kilnUpdateStopped\":\"{latest} is here; this server has {current}. The server is stopped, so the update only installs.\",\"stat\":{\"players\":\"Players\",\"notRunning\":\"Not running\",\"health\":\"Health\",\"stepsPerS\":\"steps/s\",\"p99\":\"Step p99 {ms} ms\",\"noTicks\":\"No step times yet\",\"uptime\":\"Uptime\",\"startedIn\":\"Started in {time}\",\"versions\":\"Kiln\",\"noKiln\":\"Not installed\",\"isOut\":\"{latest} is here.\",\"game\":\"Game {version}.\"},\"sections\":\"Sections\",\"link\":{\"consoleLive\":\"Live, with Kiln commands\",\"consoleLast\":\"The last run's output\",\"playersOff\":\"When it runs\",\"mods\":{\"one\":\"{count} mod\",\"other\":\"{count} mods\"},\"backups\":{\"one\":\"{count} backup, last {ago}\",\"other\":\"{count} backups, last {ago}\"},\"noBackups\":\"No backup yet\",\"settings\":\"UDP {port}, difficulty, groups\"}},\"head\":{\"installKiln\":\"Install Kiln\",\"navLabel\":\"Server\",\"durH\":\"{h} h {m} min\",\"durM\":\"{m} min {s} s\",\"durS\":\"{s} s\",\"nobody\":\"Nobody online\",\"online\":\"{count} online\",\"nobodyLine\":\"Nobody online.\",\"onlineLine\":\"{count} online.\",\"slotsFree\":{\"one\":\"{who}, {count} slot free.\",\"other\":\"{who}, {count} slots free.\"},\"reading\":\"Reading the server's state.\",\"running\":\"Running.\",\"runningFor\":\"Running for {time}.\",\"starting\":\"Starting.\",\"stopping\":\"Stopping.\",\"stoppedWithKiln\":\"Stopped. Start runs it with {runner}, Kiln {kiln}.\",\"stoppedWith\":\"Stopped. Start runs it with {runner}.\",\"stoppedKiln\":\"Stopped. Start runs it with Kiln {kiln}.\",\"stopped\":\"Stopped. Press Start to run it.\",\"updateGame\":\"Update the game (SteamCMD)\",\"upToDate\":\"Server files are up to date\",\"updateKilnMenu\":\"Update Kiln…\",\"installKilnMenu\":\"Install Kiln…\",\"removeMenu\":\"Remove from the launcher\",\"changeServer\":\"Server: {name}. Change server.\",\"restarted\":\"Server restarted\",\"restart\":\"Restart\",\"stoppedToast\":\"Server stopped\",\"stop\":\"Stop\",\"runningToast\":\"Server running\",\"start\":\"Start\",\"more\":\"More server actions\",\"kilnInstalled\":\"Kiln {version} installed\",\"removeTitle\":\"Remove this server?\",\"removeText\":\"The launcher forgets it. Its game files, saves and backups stay on disk.\",\"removed\":\"Removed {name}\",\"filesStay\":\"The files stay.\",\"notRemoved\":\"Not removed\",\"fileBridge\":\"file bridge\",\"auto\":\"auto\",\"connected\":\"{connected} {transport}, {ops} ops.\",\"connectedWord\":\"Connected.\",\"connecting\":\"Connecting to Kiln.Admin.\",\"notAnswering\":\"Kiln.Admin is not answering.\",\"transportAuto\":\"Auto (TCP, then file bridge)\",\"transportTcp\":\"KAP/1 over TCP\",\"reconnect\":\"Reconnect to Kiln.Admin\",\"connectOver\":\"Connect over: {transport}\",\"allServers\":\"All servers\",\"updateKiln\":\"Update Kiln\",\"bundle\":\"Runtime bundle\",\"bundleHint\":\"The server must be stopped.\",\"bundleOption\":\"{version} (Kiln {kiln})\"},\"list\":{\"running\":\"Running\",\"starting\":\"Starting\",\"stopping\":\"Stopping\",\"stopped\":\"Stopped\",\"created\":\"Server {name} created\",\"newTitle\":\"New dedicated server\",\"id\":\"Id\",\"idHint\":\"Lower-case letters, digits, - and _ (also the kilnctl instance name).\",\"idBad\":\"Not a valid id\",\"name\":\"Name\",\"nameHint\":\"Shown in the server browser.\",\"folder\":\"Folder\",\"folderHint\":\"An empty folder; SteamCMD installs the server here.\",\"port\":\"UDP port\",\"portHint\":\"The query port players connect to.\",\"copyFrom\":\"Copy game files from\",\"copyFromHint\":\"Optional: an existing install on the same btrfs/XFS disk (reflink, no extra space). SteamCMD then only checks the files.\",\"policy\":\"Kiln policy\",\"policyVerified\":\"verified-only: hooks on verified game builds only\",\"policyAny\":\"any: also unverified builds (labs)\",\"skipSteamcmd\":\"Skip SteamCMD\",\"skipSteamcmdHint\":\"The copied files are up to date.\",\"imported\":\"Imported {name}\",\"importFailed\":\"Import failed\",\"importTitle\":\"Import a server folder\",\"import\":\"Import\",\"serverFolder\":\"Server folder\",\"serverFolderHint\":\"The folder with enshrouded_server.exe. A Steam library install is started through Steam; any other folder becomes a kilnctl instance.\",\"serverFolderHintWin\":\"The folder with enshrouded_server.exe. A Steam library install is started through Steam; any other folder becomes a Windows server the launcher starts.\",\"server\":\"Server\",\"runsAs\":\"Runs as\",\"foundInSteam\":\"{runner}, found in Steam\",\"portCol\":\"Port\",\"online\":\"Online\",\"state\":\"State\",\"reading\":\"Reading the servers.\",\"noneYet\":\"None yet. Install one with SteamCMD, or import a server folder.\",\"count\":{\"one\":\"{count} dedicated server on this PC and over SSH.\",\"other\":\"{count} dedicated servers on this PC and over SSH.\"},\"title\":\"Servers\",\"newServer\":\"New server\",\"noServers\":\"No servers\",\"noServersHint\":\"Install one with SteamCMD, or import a folder.\",\"namePlaceholder\":\"Ember Isles\",\"durD\":\"{d} d {h} h\",\"backupTitle\":\"Backing up {name}\",\"stepBackup\":\"Back up the save\",\"health\":\"Health\",\"kiln\":\"Kiln\",\"runsOn\":\"Runs on\",\"actions\":\"Quick actions\",\"overSsh\":\"{host} over SSH\",\"since\":\"since {time}\",\"updateWord\":\"Update\",\"updateTitle\":\"Kiln {version} is out\",\"perSecond\":\"{rate}/s\",\"healthTitle\":\"Steps per second: {rate} in the last minute\",\"healthLabel\":{\"one\":\"Steps per second over the last minute: {rate}\",\"other\":\"Steps per second over the last {count} minutes, {rate} now\"},\"console\":\"Console\",\"moreFor\":\"More actions for {name}\",\"startNamed\":\"Start {name}\",\"stopNamed\":\"Stop {name}\",\"consoleNamed\":\"Open the console of {name}\",\"updateNamed\":\"Update Kiln on {name}\",\"stepOf\":\"{n} of {count}: {step}\",\"word\":{\"starting\":\"Starting\",\"stopping\":\"Stopping\",\"restarting\":\"Restarting\",\"updating\":\"Updating\",\"installing\":\"Installing\",\"backingUp\":\"Backing up\"},\"say\":{\"count\":{\"one\":\"{count} server.\",\"other\":\"{count} servers.\"},\"full\":\"{count} {rest}\",\"join1\":\"{a}.\",\"join2\":\"{a}; {b}.\",\"join3\":\"{a}; {b}; {c}.\",\"busyStart\":\"{name} is starting\",\"busyStop\":\"{name} is stopping\",\"busyRestart\":\"{name} is restarting\",\"busyUpdate\":\"{name} is updating its game files\",\"busyDeploy\":\"{name} is updating Kiln\",\"busyCreate\":\"{name} is being installed\",\"busyBackup\":\"{name} is being backed up\",\"busyMany\":\"{count} servers are busy\",\"runningPlayers\":{\"one\":\"{name} is running with {count} player\",\"other\":\"{name} is running with {count} players\"},\"runningNobody\":\"{name} is running with nobody on it\",\"runningMany\":{\"one\":\"{servers} are running with {count} player in all\",\"other\":\"{servers} are running with {count} players in all\"},\"runningManyNobody\":\"{servers} are running with nobody on them\",\"noneRunning\":\"None is running\",\"updateOne\":\"{name} has a Kiln update\",\"updateMany\":\"{count} have a Kiln update\"},\"banner\":{\"title\":\"Update\",\"updatingTitle\":\"Updating\",\"failedTitle\":\"Failed\",\"single\":\"Kiln {version} is out. {name} runs {current}; it is stopped, so only Kiln changes.\",\"oneRunning\":\"Kiln {version} is out. {name} runs {current}; it is running, so it stops, updates and starts again.\",\"several\":\"Kiln {version} is out. {names} run an older Kiln.\",\"updateOne\":\"Update {name}\",\"updateAll\":\"Update all {count}\",\"running\":\"Updating {name}, step {n} of {count}: {step}.\",\"failed\":\"The Kiln update of {name} failed: {reason}\"}},\"modpackOffer\":{\"thisServer\":\"this server\",\"installed\":\"Modpack installed\",\"launchFailed\":\"Launch failed\",\"usesMods\":\"{name} uses mods\",\"launch\":\"Launch\",\"notNow\":\"Not now\",\"downloadInstall\":\"Download and install\",\"profile\":\"Profile\",\"mods\":\"Mods\",\"newInLibrary\":\"{n} new in the library\",\"saveBackup\":\"Save backup\",\"loadedHint\":\"{profile} is loaded: Play starts the game with these mods, then join {name} again.\",\"noPack\":\"No modpack\",\"noPackText\":\"{name} did not say where to get its mods. Ask its admin.\",\"refused\":\"The server refused the join\",\"modsMissing\":\"Mods are missing.\",\"server\":\"Server\",\"serverWithId\":\"{name} ({id})\",\"modpack\":\"Modpack\",\"from\":\"From\",\"size\":\"Size\",\"unknown\":\"unknown\",\"cannotCheck\":\"Cannot check it\",\"missingHere\":\"Missing here: {list}.\",\"allThere\":\"Everything it lists is in your library already.\",\"nativeCode\":\"It contains native code (DLLs).\",\"notHttps\":\"Not https\",\"notHttpsText\":\"This modpack comes over plain http. Allow http modpacks in Settings to take it.\",\"runsCode\":\"This runs code on your PC\",\"runsCodeText\":\"Mods are programs. Install only from servers you trust. The download is checked against the hashes the server sent.\",\"trust\":\"I trust {name} and want its mods\"},\"modpack\":{\"built\":\"Modpack built\",\"published\":\"Published: players who join now get the offer\",\"notPublished\":\"Not published\",\"mod\":\"Mod\",\"kind\":\"Kind\",\"needTitle\":\"What players need\",\"needSub\":\"Mods whose manifest says clients: \\\"mod\\\"; the server checks these hashes when a player joins.\",\"needEmpty\":\"Nothing: vanilla players and any Kiln client can join\",\"required\":\"Required mods\",\"buildTitle\":\"Build\",\"buildSub\":\"A deterministic zip: the same mods give the same file and sha256.\",\"name\":\"Name\",\"version\":\"Version\",\"file\":\"File\",\"size\":\"Size\",\"mods\":\"Mods\",\"publish\":\"Publish\",\"publishSub\":\"Upload the zip somewhere players can download it (https), then enter its URL.\",\"stale\":\"The published modpack is not the last build\",\"staleText\":\"Publish again after uploading the new zip.\",\"publishedUrl\":\"Published URL\",\"downloadUrl\":\"Download URL\",\"httpHint\":\"Players' launchers refuse http:// unless they allow it in Settings.\"},\"mods\":{\"selfOff\":\"Kiln.Admin serves the live pages; switching it off would cut them.\",\"litName\":\"Lit: {name}\",\"putOutName\":\"Put out: {name}\",\"restartToApply\":\"Restart the server to apply.\",\"nextStart\":\"Applies at the next start.\",\"sayLive\":\"{loaded} of {total} loaded, {ms} ms a tick ({pct}%). The flame decides what loads at the next start.\",\"sayStopped\":\"The flame decides what loads at the next start. Reload, unload and tests need the server running.\",\"litNext\":\"Lit at the next start\",\"lit\":\"Lit\",\"out\":\"Out\",\"msTick\":\"ms/tick\",\"title\":\"Mods\",\"syncTitle\":\"Sync from the library\",\"files\":\"Files\",\"filter\":\"Filter mods\",\"shownOf\":\"{shown} of {total}\",\"serverMods\":\"Server mods\",\"reading\":\"Reading kiln/mods.\",\"empty\":\"No mods in kiln/mods. Sync some from the library.\",\"selected\":\"Selected mod\",\"pick\":\"Pick a mod.\",\"startToAct\":\"Start the server to reload, unload or test it.\",\"failed\":\"Failed\",\"note\":\"Note\",\"lastFault\":\"Last fault\",\"nextStartLabel\":\"Next start\",\"loads\":\"Loads\",\"off\":\"Off\",\"loadOrder\":\"Load order\",\"cost\":\"Cost\",\"costValue\":\"{ms} ms a tick, peak {peak}\",\"faults\":\"Faults\",\"needs\":\"Needs\",\"playersNeed\":\"Players need\",\"theMod\":\"The mod\",\"nothing\":\"Nothing\",\"from\":\"From\",\"fromLibrary\":\"library +{hash}\",\"byHand\":\"Put there by hand\",\"folder\":\"Folder\",\"advanced\":\"Advanced (hooks or patches)\",\"steam\":\"Steam\",\"steamMessages\":\"Talks to other players over Steam\",\"role\":\"Role\",\"servesLive\":\"Serves the live pages\",\"also\":\"Also {list}\",\"unloadTitle\":\"Unload {name}?\",\"unloadText\":\"It stops at the start of the next tick, with the mods that depend on it. Load it again here, or restart the server.\",\"unload\":\"Unload\",\"noTestsFor\":\"{name} has no tests\",\"testLine\":\"{name}: {passed} passed, {failed} failed, {skipped} skipped\",\"runTestsHint\":\"Run this mod's tests in the running server\",\"runTests\":\"Run tests\",\"reloadHint\":\"Unload and load again from disk\",\"reload\":\"Reload\",\"tryAgain\":\"Try again\",\"load\":\"Load\",\"noTests\":\"No tests\",\"failing\":\"Failing\",\"passing\":\"Passing\",\"skipped\":\"Skipped\",\"testSummary\":\"{passed} passed, {failed} failed, {skipped} skipped in {sec} s, {ago}.\",\"synced\":\"Mods synced\",\"sync\":\"Sync\",\"syncHint\":\"The server gets exactly the checked versions. Mods put there by hand stay.\",\"libraryEmpty\":\"The library is empty: drop mods on the launcher window; they show in the list on Play.\",\"handPlaced\":\"A folder put there by hand has this id.\",\"versionOf\":\"Version of {name}\"},\"console\":{\"copied\":{\"one\":\"Copied {count} line\",\"other\":\"Copied {count} lines\"},\"copyFailed\":\"Copy failed\",\"gameLog\":\"game log\",\"console\":\"console\",\"noWarnings\":\"No warnings in this run.\",\"noErrors\":\"No errors in this run.\",\"noMatch\":\"Nothing matches that filter.\",\"nothingYet\":\"Nothing written yet.\",\"level\":\"Level\",\"all\":\"All\",\"warnings\":\"Warnings\",\"errors\":\"Errors\",\"pausedNew\":\"Paused, {n} new\",\"pause\":\"Pause\",\"filter\":\"Filter\",\"more\":\"More for the console\",\"copyLines\":{\"one\":\"Copy {count} line\",\"other\":\"Copy {count} lines\"},\"kilnCommands\":\"Kiln commands\",\"outside\":\"Started outside the launcher: this is its game log. Start it here for the full output.\",\"noConsole\":\"No console\",\"noOutput\":\"No output yet. Start the server: its output shows here, without a console window.\",\"label\":\"Server console\",\"truncated\":\"Older lines are left out.\",\"follow\":\"Follow\",\"connecting\":\"Connecting to Kiln.Admin…\",\"startToSend\":\"Start the server to send Kiln commands\",\"hint\":\"command, e.g. help (Tab completes, Up and Down go through the history)\",\"noRunOp\":\"This Kiln.Admin has no command_run op\",\"notAnswering\":\"Kiln.Admin is not answering: {error}\",\"notConnected\":\"Kiln.Admin is not connected\",\"echoAs\":\"[as {who}] > {line}\",\"denied\":\"Denied: {error}\",\"noPermission\":\"no permission\",\"unknown\":\"Unknown command. {error}\",\"runAs\":\"Run as\",\"asAdmin\":\"as admin\",\"asPlayer\":\"as {name}\",\"running\":\"Running\",\"completions\":\"Completions\",\"commands\":\"Commands\",\"noCommand\":\"No command matches that.\",\"also\":\"also {list}\",\"from\":\"{description} From {owner}.\",\"noDescription\":\"No description.\",\"needs\":\"Needs {node} {permission}\"},\"activity\":{\"done\":\"Done\",\"failed\":\"Failed\",\"canceled\":\"Canceled\",\"timeTitle\":\"Time taken\",\"dismiss\":\"Dismiss\",\"step\":{\"pending\":\"waiting\",\"running\":\"running\",\"done\":\"done\",\"failed\":\"failed\",\"canceled\":\"canceled\",\"skipped\":\"skipped\"},\"waiting\":\"Waiting for the first line from the server…\",\"noReason\":\"It stopped without saying why. The console may show more.\",\"tryAgain\":\"Try again\",\"console\":\"Open the console\",\"hideDetails\":\"Hide details\",\"showDetails\":{\"one\":\"Show details ({count} line)\",\"other\":\"Show details ({count} lines)\"},\"startTitle\":\"Starting {name}\",\"stopTitle\":\"Stopping {name}\",\"restartTitle\":\"Restarting {name}\",\"updateGameTitle\":\"Updating {name}'s game files\",\"deployTitle\":\"Installing Kiln {version} on {name}\",\"updateKilnTitle\":\"Updating {name} to Kiln {version}\",\"stepStop\":\"Stop the server\",\"stepStart\":\"Start the server\",\"stepStartAgain\":\"Start it again\",\"stepRestart\":\"Restart the server\",\"stepUpdateGame\":\"Update the game files (SteamCMD)\",\"stepDeploy\":\"Install Kiln {version}\",\"stoppingButton\":\"Stopping…\",\"installedNow\":\"Installed now: Kiln {version}.\",\"notInstalled\":\"Kiln is not installed on this server yet.\",\"bundleHint\":\"The Kiln version to install.\",\"runningNote\":\"The server is running: it is stopped (the world is saved), Kiln is installed, then it starts again.\",\"startAfter\":\"Start the server afterwards\",\"planLabel\":\"What happens\",\"installStopsFirst\":\"The server is running: installing stops it first and starts it again afterwards.\",\"updating\":\"Updating.\",\"busyButton\":\"Working…\",\"stopWaiting\":\"Stop waiting\",\"cancel\":\"Cancel\",\"srRunning\":\"{title}: {step}.\",\"srDone\":\"{title}: {result}.\",\"srFailed\":\"{title} failed: {reason}\",\"srCanceled\":\"{title}: canceled.\",\"linesLabel\":\"Every line of this action\",\"failedMarker\":\"The last action failed: {reason}\"},\"frame\":{\"live\":\"live\",\"allServers\":\"‹ All servers\",\"switch\":\"Other servers\",\"switchItem\":\"{name}, {state}\",\"unknown\":\"State unknown\",\"updating\":\"Updating\"},\"recent\":{\"title\":\"Recent\",\"hint\":\"What this launcher did on any server, newest first\",\"label\":\"Recent actions\",\"none\":\"Nothing yet. Starts, stops, Kiln updates and backups show here.\",\"noReason\":\"no reason given\",\"what\":{\"start\":\"Start\",\"stop\":\"Stop\",\"restart\":\"Restart\",\"update\":\"Game update\",\"deploy\":\"Kiln install\",\"create\":\"Install\",\"backup\":\"Backup\",\"remove\":\"Remove\"},\"step\":{\"stop\":\"stop\",\"deploy\":\"install\",\"start\":\"start\",\"update\":\"update\",\"create\":\"install\"},\"started\":\"Started in {time}.\",\"startedKiln\":\"Started in {time}, Kiln {kiln}.\",\"stopped\":\"Stopped by you. Saved in {time}.\",\"restarted\":\"Restarted in {time}.\",\"gameUpdated\":\"Game files updated in {time}.\",\"kilnUpdated\":\"Kiln updated from {from} to {kiln} ({steps}: {time}).\",\"kilnInstalled\":\"Kiln {kiln} installed in {time}.\",\"kilnInstalledSteps\":\"Kiln {kiln} installed ({steps}: {time}).\",\"created\":\"Installed in {time}.\",\"createdKiln\":\"Installed with Kiln {kiln} in {time}.\",\"createdStarted\":\"Installed and started in {time}.\",\"createdKilnStarted\":\"Installed with Kiln {kiln} and started in {time}.\",\"backedUp\":\"Backed up in {time}.\",\"removed\":\"Removed from the launcher. Its files stay on disk.\",\"failed\":\"{what} failed after {time}: {reason}\",\"canceled\":\"{what} canceled after {time}.\"},\"wizard\":{\"title\":\"New server\",\"flowTitle\":\"Installing {name}\",\"stepper\":\"Steps\",\"s1\":\"Where and what\",\"s2\":\"Install\",\"s3\":\"Install Kiln\",\"s4\":\"Start\",\"notChosen\":\"(not chosen)\",\"stepCreate\":\"Install the server files (SteamCMD)\",\"stepCopy\":\"Copy the game files and check them (SteamCMD)\",\"stepCopyOnly\":\"Copy the game files\",\"idTaken\":\"{name} has this id already.\",\"nameLongBytes\":\"Too long: a name has at most 64 bytes (letters beyond A to Z take 2 to 4 each).\",\"idMadeUp\":\"The name has no letters an id can use, so this one is made up. Type your own if you like.\",\"folderBad\":\"An absolute folder: it starts with / (or a drive letter on Windows).\",\"portBad\":\"A port from 1024 to 65535.\",\"portFree\":\"Free\",\"portTakenWord\":\"Taken\",\"portTaken\":\"{name} uses this port too: the two cannot run at the same time.\",\"copyNone\":\"Nothing: SteamCMD downloads the files\",\"copyServer\":\"{name}: {dir}\",\"copyOther\":\"Another folder…\",\"copyFolder\":\"Folder to copy from\",\"kilnToggle\":\"Install Kiln {version} when the files are in\",\"kilnHint\":\"Recommended. You can add it later from the server's ··· menu.\",\"kilnNone\":\"Install Kiln when the files are in\",\"kilnNoneHint\":\"No Kiln runtime bundle yet: import one on the Runtime page first.\",\"startToggle\":\"Start the server when it is ready\",\"keepsGoing\":\"Close this and it keeps going: its row in the list shows it.\",\"cancelInstall\":\"Cancel install\",\"elapsed\":\"Elapsed\",\"ready\":\"The server is ready.\",\"readyRunning\":\"The server is ready and running.\",\"readyHint\":\"All done in {time}. Its page has the console, the settings and the mods.\",\"open\":\"Open the server\",\"failedAt\":\"It stopped at: {step}.\",\"halfMadeRetry\":\"The launcher now lists {id}, half made. Try again finishes it in the same folder; or remove it from its ··· menu (its files stay) and install again.\"}}"),
	play: {
		label: "Play",
		noGame: {
			firstStart: "First start",
			noClient: "No client found",
			notFound: "Not found",
			looking: "Looking for Enshrouded in your Steam libraries.",
			notFoundText: "Kiln did not find Enshrouded in your Steam libraries.",
			addFolder: "Add a folder",
			lookAgain: "Look again"
		},
		addGame: {
			added: "Game added",
			title: "Add a game folder",
			folder: "Folder",
			folderHint: "The folder with enshrouded.exe: Steam's copy, or any other copy of the game, for example an older build kept for mods.",
			addedCustom: "Game folder added: Kiln starts it directly"
		},
		proxy: {
			kiln: "In place",
			none: "None",
			emf: "EMF's own",
			unknown: "Another DLL mod",
			notdll: "Not a DLL",
			chainloader: "A chain loader",
			nativefolders: "CharosDX's mod loader"
		},
		detected: "{count} found",
		say: {
			checking: "Checking the game.",
			installing: "Installing Kiln.",
			shroudforge: "ShroudForge is in this game. Switch on Runtime to let Kiln take over its dinput8.dll.",
			notInstalled: "Kiln is not in this game yet.",
			gettingReady: "Getting your mod list ready.",
			noProfile: "No profile is loaded: the mods in the game folder load as they are.",
			unreadable: "Your mods are not readable: {error}",
			reading: "Reading your mods.",
			noMods: "No mods yet."
		},
		run: {
			stopped: "Kiln could not go on: {why}.",
			stoppedSeeLog: "Kiln could not go on: see kiln.log on the Logs page.",
			safeIdle: "Kiln is in safe idle: {why}.",
			safeIdleOff: "Kiln is in safe idle: mods stay off.",
			vanilla: "Started without Kiln.",
			kilnMissing: "Running without Kiln: Steam did not load it this start.",
			noBootRecord: "The game is running. No Kiln boot record for this start.",
			noMods: "Running with Kiln {version} and no mods.",
			with: "Running with Kiln {version}.",
			withMods: {
				one: "Running with Kiln {version} and {count} mod.",
				other: "Running with Kiln {version} and {count} mods."
			},
			kilnMissingDirect: "Running without Kiln: the game did not load Kiln's proxy this start."
		},
		stage: {
			game: "Game",
			mods: "Mods",
			start: "Start"
		},
		lastBoot: {
			time: "{seconds} s, {problems}",
			done: "Done, {problems}",
			notYet: "Not yet"
		},
		kilnNote: {
			failed: "update failed",
			failedTip: "{what} failed: {error}",
			beta: "from the beta",
			betaTip: "Play installs Kiln {version} first (this launcher's)",
			ready: "update ready",
			readyTip: "{version} is ready to install"
		},
		build: "build {build}",
		buildVerified: "build {build}, verified",
		buildUnknown: "build unknown",
		game: "Game",
		profileTip: "The loaded profile: Settings, Profiles",
		profile: "Profile: {name}",
		noProfileLoaded: "No profile loaded",
		waiting: "Waiting for Kiln to boot.",
		bootStages: "Boot stages",
		startedAgo: "Started {time} ago. Switches apply at the next start.",
		startedAgoDetail: "Started {time} ago · {detail}. Switches apply at the next start.",
		justOnce: "Just once:",
		vanilla: "Vanilla",
		withoutMods: "Without mods",
		installKiln: "Install Kiln",
		playVanilla: "Play vanilla",
		notInstalled: "Not installed",
		proxyLabel: "Proxy",
		runsOn: "Runs on",
		lastBootLabel: "Last boot",
		lastBootOpen: "Last boot: {text}. Open the logs.",
		detect: "Detect",
		modpack: {
			title: "Modpack",
			server: "Server",
			missing: "Missing",
			url: "URL",
			notAvailable: "Modpack install is not available in this build."
		},
		banner: {
			refused: "Refused",
			getModpack: "Get modpack",
			aServer: "A server",
			refusedReason: "{server}: {reason}",
			refusedMissing: "{server}: missing {mods}.",
			refusedNeeds: "{server}: it needs mods you do not have.",
			changeFailed: "{what} failed",
			runtime: "Runtime",
			noReason: "It stopped without a reason.",
			stillHas: "The game still has Kiln {version}.",
			notInGame: "Kiln is not in the game.",
			updating: "Updating",
			steamUpdating: "Steam is updating Enshrouded. Play waits until it is done.",
			fromBeta: "Kiln from the beta",
			fromRelease: "Kiln from the release",
			updateKiln: "Update Kiln",
			needsNewer: {
				one: "{names} needs a newer Kiln.",
				other: "{names} need a newer Kiln."
			},
			graphicsTool: "{name} is installed ({file})",
			settingAsi: "One Steam setting for Ultimate ASI Loader",
			notLoading: "Kiln is not loading",
			didNotStart: "Kiln did not start",
			setting: "One Steam setting",
			noClipboard: "Clipboard unavailable",
			copied: "Copied",
			copiedDetail: "Paste it into Steam's launch options.",
			copyFailed: "Could not copy the launch option",
			adviceStuck: "The game started without Kiln, so mods and \"Without mods\" can't apply. Close it, then set Steam, Enshrouded, Properties, Launch options: {option}",
			adviceAsi: "Kiln loads, but Ultimate ASI Loader and its .asi plugins may not start under Valve’s Proton. Steam, Enshrouded, Properties, Launch options: {option}",
			advice: "Steam, Enshrouded, Properties, Launch options: {option}",
			supportReport: "Support report",
			noKiln: "The game started without Kiln. Close it, then save a support report on the Logs page and send us that zip: it explains most causes itself.",
			safeIdle: "Safe idle",
			noModsLoaded: "No mods loaded",
			profileInvalid: "profile.json is invalid: no mods loaded."
		},
		vanillaOnce: {
			title: "Play vanilla once",
			text: "A character carrying items from a Kiln mod can crash on a server that lets vanilla clients in, and may lose those items on other servers. Use another character."
		},
		problems: {
			none: "no errors",
			didNotLoad: "{file} did not load",
			plugin: "plugin {id}: {error}",
			failed: "failed",
			modsFailed: {
				one: "{count} mod failed",
				other: "{count} mods failed"
			},
			nativeFailed: {
				one: "{count} native mod failed",
				other: "{count} native mods failed"
			},
			errors: {
				one: "{count} error",
				other: "{count} errors"
			},
			errorsSinceStart: {
				one: "{count} error since start",
				other: "{count} errors since start"
			},
			more: "… {count} more in kiln.log"
		},
		copyMode: "{platform}, copy mode",
		names: {
			pair: "{0} and {1}",
			more: "{a}, {b} and {count} more"
		},
		source: { custom: "custom folder" },
		startsVia: "Starts {how}"
	},
	catalog: {
		updateTo: "Update {version}",
		installed: "Installed",
		noAnswer: "Nexus Mods did not answer",
		countKiln: {
			one: "{count} Kiln mod on Nexus Mods.",
			other: "{count} Kiln mods on Nexus Mods."
		},
		countInstalled: {
			one: "{count} installed mod on Nexus Mods.",
			other: "{count} installed mods on Nexus Mods."
		},
		countAll: {
			one: "{count} Enshrouded mod on Nexus Mods.",
			other: "{count} Enshrouded mods on Nexus Mods."
		},
		lookingMore: "Looking at {count} more.",
		asking: "Asking Nexus Mods.",
		signInToFind: "Sign in to Nexus Mods to find Kiln mods.",
		title: "Catalog",
		signIn: "Sign in to Nexus Mods",
		settings: "Settings",
		connect: "Connect your account to find Kiln mods and download them.",
		signInAgain: "Sign in to Nexus Mods again",
		accountCheckFailed: "Nexus Mods could not check your account",
		updateReady: "Update ready: {name}",
		updatesReady: "{count} updates ready",
		openPage: "Open page",
		updateLine: "{name} {from} → {to}",
		updateLineStale: "{name} {from} → {to} (not confirmed lately)",
		newUpload: "a new upload",
		showLabel: "Show",
		filterKiln: "Kiln mods",
		filterAll: "All Enshrouded",
		filterInstalledCount: "Installed ({count})",
		sort: "Sort",
		sortEndorsed: "Most endorsed",
		sortUpdated: "Recently updated",
		sortNew: "Newest",
		sortDownloads: "Most downloaded",
		sortName: "Name",
		search: "Search",
		tryAgain: "Try again",
		signInShort: "Sign in to find Kiln mods",
		signInShortHint: "Every Enshrouded mod is under All Enshrouded.",
		showAll: "Show all",
		noMatch: "No mod matches that.",
		noneInstalled: "Nothing installed from Nexus Mods yet.",
		looking: "Looking for Kiln mods…",
		noMods: "No mods here yet.",
		mods: "Mods",
		previous: "Previous",
		page: "Page {page} of {pages}",
		next: "Next",
		endorsements: "Endorsements",
		downloads: "Downloads",
		opened: "Opened on Nexus Mods",
		openedDetail: "Click Mod Manager Download there.",
		updateOnNexus: "Update on Nexus Mods",
		downloadOnNexus: "Download on Nexus Mods",
		updateToVersion: "Update to {version}",
		updateToPlain: "Update to",
		addAgain: "Add again",
		mod: "Mod",
		openOnNexus: "Open on Nexus Mods",
		notAvailable: "Not available",
		picture: "Picture {n}",
		author: "Author",
		uploadedBy: "{credit} (uploaded by {author})",
		version: "Version",
		notInLibrary: "{version} (not in the library any more)",
		downloadsUnique: "{downloads} ({unique} unique)",
		updated: "Updated",
		needsAuth: "Files and requirements need your authorization",
		needsAuthText: "Add it in Settings.",
		requires: "Requires",
		otherGame: "Other game",
		notInstalled: "Not installed",
		files: "Files",
		addTakesThis: "Add takes this",
		mayNotLoad: "Kiln may not load this",
		mayNotLoadText: "No kiln.mod.json, native.json or DLL in its files.",
		freeAccounts: "Free accounts download on the Nexus Mods site. Its Mod Manager Download brings the mod back here (Settings, Nexus Mods)."
	},
	runtime: {
		bundleAdded: "Bundle {version} added",
		title: "Runtime",
		noGame: "No game yet. Find Enshrouded on Play first.",
		version: "Version",
		installed: "Installed",
		built: "Built",
		parts: "Parts",
		action: "Action",
		reinstall: "Reinstall",
		rollBack: "Roll back",
		inGameUpdated: "Kiln {version} is in the game folder: {files} files, updated {ago}.",
		inGameInstalled: "Kiln {version} is in the game folder: {files} files, installed {ago}.",
		readyToInstall: "{version} is ready to install.",
		reading: "Reading the game folder.",
		shroudforge: "ShroudForge is in this game. Kiln can take over its dinput8.dll.",
		notInstalled: "Kiln is not in this game yet.",
		uninstall: "Uninstall",
		running: "Running",
		runningText: "Close the game to install or remove Kiln.",
		updating: "Updating",
		updatingText: "Steam has an update queued for Enshrouded. Installs wait until it is done.",
		switchSf: "Switch from ShroudForge",
		sfText: "Kiln takes over its dinput8.dll (kept in backups, put back on uninstall). ShroudForge's own dbghelp.dll, winmm.dll and folder stay where they are.",
		changed: "Changed",
		changedText: {
			one: "{count} file changed since the install: {files}",
			other: "{count} files changed since the install: {files}"
		},
		partsMissing: "Parts missing",
		partsMissingText: "Kiln {version} has parts this game does not: {parts}. Mods that need them do not load until they are in.",
		repair: "Repair",
		filesMissing: "Files missing",
		filesMissingText: {
			one: "{count} of Kiln's files is missing: {files}",
			other: "{count} of Kiln's files are missing: {files}"
		},
		filesMissingAntivirus: {
			one: "An antivirus may have removed it.",
			other: "An antivirus may have removed them."
		},
		filesMissingRepair: {
			one: "Repair puts it back.",
			other: "Repair puts them back."
		},
		filesMissingNoBundle: {
			one: "Kiln {version} is not among the bundles below: update Kiln, or add its bundle, to put it back.",
			other: "Kiln {version} is not among the bundles below: update Kiln, or add its bundle, to put them back."
		},
		antivirusHelp: "Antivirus help",
		chainTitle: "What the game loads, in order",
		chainLive: "Live, from this boot.",
		chainNotLive: "Live statuses show after a start.",
		stateUnreadable: "The game's state is not readable",
		partsSub: "Kiln {version}: what is in this game and what else the bundle has.",
		partsSubNoBundle: "What is in this game (the bundle it came from is not in the launcher).",
		bundles: "Bundles in the launcher",
		bundleCount: {
			one: "{count} bundle",
			other: "{count} bundles"
		},
		bundlesUnreadable: "Bundles are not readable",
		bundlesLabel: "Runtime bundles",
		noBundles: "No bundles. Add a kiln-runtime zip below.",
		addBundle: "Add a runtime bundle",
		readingBundle: "Reading the bundle…",
		bundleFilter: "Kiln runtime",
		uninstallAsk: "Uninstall Kiln?",
		uninstallFiles: "Kiln's unchanged files leave the game folder and the files it replaced come back, such as the original dinput8.dll, unless another loader's file has taken its name since: that one stays, and the result says where the original is. Unchanged mod copies it placed outside kiln/ go too; changed ones stay and are listed. kiln/ (configs, data, logs) stays.",
		uninstallEml: "Game archives EML patched stay patched. To undo that, switch the EML mods off and start the game once through Kiln first; that restores only archives Kiln's EML plugin wrote and that are unchanged since. Archives another loader patched stay as they are.",
		removed: "Kiln removed",
		switchSfAsk: "Switch from ShroudForge?",
		switch: "Switch",
		switchSfText: "ShroudForge's dinput8.dll moves to {folder} and Kiln {version} goes in. The rest of ShroudForge stays in place.",
		part: {
			core: "proxy, core, .NET host",
			sigdbClient: "signatures for the game",
			sigdbServer: "signatures for servers",
			admin: "Kiln.Admin (servers)",
			emf: "runs EMF mods (kiln-emf)",
			eml: "runs EML mods (kiln-eml, GPL-3.0)",
			shroudforge: "runs ShroudForge mods (its runtime, GPL-3.0)",
			enshwick: "runs Enshwick mods (Enshwick's loader)",
			samples: "sample mods"
		},
		state: {
			missing: "Missing",
			off: "Turned off",
			optional: "Optional"
		},
		plan: {
			installs: "Installs",
			keeps: "Keeps",
			adds: "Adds",
			offChosen: "Off, your choice",
			removes: "Removes",
			off: "Off"
		},
		installTitle: "Install Kiln {version}",
		keepParts: "Keep my parts",
		defaultParts: "Default parts",
		keepPartsHint: "Keeps your parts and adds defaults you have not turned off. Off: choose below.",
		defaultPartsHint: "Core, signatures and the parts for EMF, EML, ShroudForge and Enshwick mods, for this side.",
		planLabel: "Parts this install puts in",
		changeFailed: "{what} failed",
		noReason: "It stopped without a reason.",
		keeps: "The game keeps Kiln {version}.",
		notInGame: "Kiln is not in the game.",
		changeDone: "{what}: done",
		isInGame: "Kiln {version} is in the game folder.",
		starting: "Starting",
		chain: {
			build: "Build {build}",
			buildVerified: "Build {build}, verified",
			game: "The game",
			kilnProxy: "Kiln proxy {version}",
			proxy: "{kind} proxy",
			system: "System DLL",
			chained: "{class}, chained",
			unknown: "unknown",
			services: "services, Kiln.Host",
			mods: "Then your mods",
			backedUp: "Backed up, not chained: {files}"
		}
	},
	settings: /* @__PURE__ */ JSON.parse("{\"title\":\"Settings\",\"sub\":\"Profiles, the library, the launcher, the games it knows, and Kiln's own settings for this game.\",\"launcher\":{\"title\":\"Launcher\",\"unavailable\":\"Settings unavailable\",\"defaultGame\":\"Default game\",\"firstFound\":\"First found\",\"savesKeep\":\"Save backups to keep\",\"savesKeepHint\":\"Per game.\",\"keepVersions\":\"Earlier versions to keep\",\"keepVersionsHint\":\"Per mod, at an update: its earlier version and the settings it had, for Roll back in its menu. 0 keeps none.\",\"addToActive\":\"Switch new mods on\",\"addToActiveHint\":\"A dropped mod is on at the next Play. Off: it joins the list switched off.\",\"folderNotes\":\"Show notes about mod folders\",\"folderNotesHint\":\"Folders Kiln left as they are, mod files it kept, your own folders: each note shows once when it is new. Off: only a folder that may still run a mod you switched off. Notes above your mods has them all.\",\"httpModpacks\":\"Allow http modpacks\",\"httpModpacksHint\":\"Off: https only.\",\"dailyUpdates\":\"Check for mod updates once a day\",\"dailyUpdatesHint\":\"Off: only when you press Check for updates above your mods. A check reads the pages your mods name (GitHub, and Nexus Mods with your sign-in) and shows a notice with a link. It never downloads or installs a mod.\",\"dailyUpdatesHintNoNexus\":\"Off: only when you press Check for updates above your mods. A check reads the GitHub pages your mods name and shows a notice with a link. It never downloads or installs a mod.\",\"steamRoots\":\"Extra Steam folders\",\"steamRootsHint\":\"Libraries Kiln does not find on its own.\"},\"appearance\":{\"nextNoGpu\":\"The next start draws without the GPU\",\"nextGpu\":\"The next start draws with the GPU\",\"title\":\"Appearance\",\"fire\":\"Animated fire\",\"fireHint\":\"The fire on Play (WebGL). Off: a plain Play button, easier on weak GPUs.\",\"noGpu\":\"Draw the launcher without the GPU\",\"fireOffSoftware\":\"Off while the launcher draws without the GPU.\",\"fireOffEnv\":\"Off for this start: KILN_LAUNCHER_NO_WEBGL is set.\",\"desktop\":\"Kiln Launcher in the app menu\",\"desktopHint\":\"With its icon, so the taskbar and dock show Kiln's icon too. Kiln keeps it pointed at this AppImage.\",\"desktopHintFile\":\"With its icon, so the taskbar and dock show Kiln's icon too. Kiln keeps it pointed at this AppImage. {file}\",\"desktopHintForeign\":\"With its icon, so the taskbar and dock show Kiln's icon too. Kiln keeps it pointed at this AppImage. {file} is not Kiln's: turning this on replaces it.\",\"desktopOn\":\"Kiln Launcher is in the app menu\",\"desktopOff\":\"Kiln Launcher is out of the app menu\"},\"sandbox\":{\"label\":\"WebKit's sandbox\",\"intro\":\"Applies at the next start. Off: the launcher's page runs without WebKit's process sandbox; it shows only the launcher's own pages and, from the internet, only images from Nexus Mods.\",\"unused\":\"This start: WebKit does not use its sandbox (its version for this launcher uses it only when WEBKIT_FORCE_SANDBOX=1 is set).\",\"on\":\"This start: WebKit's sandbox is on (WEBKIT_FORCE_SANDBOX=1 is set).\",\"off\":\"This start: WebKit's sandbox is off.\",\"argOff\":\"This start: off, because {flag} was given; it wins over this switch.\",\"argOn\":\"This start: {flag} was given and wins over this switch.\",\"env\":\"This start: KILN_LAUNCHER_WEBKIT_SANDBOX is set and wins over this switch.\",\"yours\":\"This start: WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS={value} is set in your environment and decides, whatever this switch says.\",\"probe\":\"On this system: {says}.\",\"nextOn\":\"The next start leaves WebKit's sandbox on\",\"nextOff\":\"The next start turns WebKit's sandbox off\"},\"render\":{\"intro\":\"For a window that stays black. Applies at the next start.\",\"noGpu\":\"This start draws without the GPU.\",\"noGpuMarker\":\"This start draws without the GPU because this switch is on, and every start does until you turn it off.\",\"gpu\":\"This start draws with the GPU.\",\"noGpuRestart\":\"This start draws without the GPU: the page did not start with it.\",\"gpuRestart\":\"This start draws with the GPU: the page did not start with it.\",\"noGpuEnv\":\"This start draws without the GPU: KILN_LAUNCHER_RENDER is set and wins over this switch.\",\"gpuEnv\":\"This start draws with the GPU: KILN_LAUNCHER_RENDER is set and wins over this switch.\",\"noGpuArg\":\"This start draws without the GPU: {flag} was given and wins over this switch.\",\"gpuArg\":\"This start draws with the GPU: {flag} was given and wins over this switch.\",\"x11\":\"Switch to X11 when drawing is slow on Wayland\",\"x11Intro\":\"On a HiDPI Wayland screen, when WebKit cannot use the GPU, the launcher starts once more through X11 (XWayland): it draws at a lower resolution, so it is much faster but less sharp. Applies at the next start.\",\"x11Wayland\":\"This start draws on Wayland.\",\"x11On\":\"This start draws through X11.\",\"x11Tried\":\"X11 was tried and did not help, so the launcher stays on Wayland. Turn this off and on again to try once more.\",\"x11Software\":\"Not used while the launcher draws without the GPU.\",\"x11Yours\":\"Your GDK_BACKEND={value} decides.\",\"x11NextOn\":\"The launcher may switch to X11 by itself\",\"x11NextOff\":\"The next start stays on Wayland\",\"force\":\"Force GPU drawing on NVIDIA\",\"forceIntro\":\"The Linux launcher bundles Ubuntu's WebKit, which draws without the GPU on every NVIDIA card. This switch lifts that block (WEBKIT_FORCE_DMABUF_RENDERER=1 with shared memory, WEBKIT_DMABUF_RENDERER_FORCE_SHM=1). It is off on clean starts: some NVIDIA drivers crash or show a black window with it. If a normal NVIDIA start fails to paint, the next start may try shared memory once and remember it if it works. Turning this switch off explicitly disables that recovery too. If a shared-memory start fails, the launcher turns it off by itself. Applies at the next start.\",\"forceOff\":\"This start leaves the block as it is.\",\"forceBlocked\":\"This start draws without the GPU because of that block: turn this on to try the GPU.\",\"forceOn\":\"This start draws past the block.\",\"forceFailed\":\"The start before with it failed, so this start forces nothing and the switch goes off.\",\"recoveryTitle\":\"Launcher drawing recovered\",\"forceRecovery\":\"The previous normal NVIDIA startup never confirmed a visible frame. This start is trying shared-memory GPU drawing once; a successful visible start is remembered for this installation and display backend.\",\"forceRecovered\":\"Shared memory is the last working automatic renderer for this installation and display backend. Turning this switch off disables automatic recovery too.\",\"forceUnguarded\":\"The startup crash guard could not be written, so this start forces nothing and the switch goes off.\",\"forceUnused\":\"Not used without NVIDIA, while drawing without the GPU, or without WebKit's DMA-BUF renderer.\",\"forceYours\":\"Your own WEBKIT_FORCE_DMABUF_RENDERER or WEBKIT_DMABUF_RENDERER_FORCE_SHM decides.\",\"forceNextOn\":\"The next start draws past Ubuntu WebKit's NVIDIA block\",\"forceNextOff\":\"The next start leaves Ubuntu WebKit's NVIDIA block alone\"},\"dev\":{\"mock\":\"Developer: mock data fallback\",\"mockHint\":\"Read-only screens show sample data where the backend is not done.\"},\"games\":{\"found\":\"{count} found\",\"added\":\"Added\",\"title\":\"Games\",\"detect\":\"Detect\",\"none\":\"None yet.\",\"forget\":\"Forget\",\"addFolder\":\"Add a folder\",\"forgetAsk\":\"Forget this game?\",\"forgetMessage\":\"Only the launcher's record goes. The game folder and Kiln stay.\",\"startTip\":\"How Kiln starts this game: through Steam, or directly\"},\"kilnJson\":{\"title\":\"Kiln\",\"sub\":\"{file} · applies at the next start\",\"notInstalled\":\"Kiln is not installed\",\"notInstalledHint\":\"Install it on Runtime first.\",\"unavailable\":\"kiln.json unavailable\",\"overrides\":\"\\\"client\\\" and \\\"server\\\" override sections are kept as they are.\"},\"graphics\":{\"title\":\"Graphics\",\"sub\":\"Kiln's own graphics tools in the game, also in the game's Kiln menu (Insert) under Graphics. The frame cap and the readout apply at once, DLSS settings when the game next sets up DLSS, the upscaler when the game next sets up FSR.\",\"aside\":{\"line\":\"{tool}: {effect}\",\"optiscaler\":\"OptiScaler is installed ({file})\",\"streamline\":\"NVIDIA Streamline is in the game folder ({file})\",\"vkfg\":\"A DLSS frame generation mod is installed ({file})\",\"dlsstweaks\":\"DLSSTweaks is installed ({file})\",\"nvngx\":\"Another DLSS loader is in the game folder ({file})\",\"unknown\":\"Another graphics tool is in the game folder ({file})\",\"frameCap\":\"Kiln's frame cap and DLSS settings step aside; the readout still works.\",\"frameCapUpscaler\":\"Kiln's frame cap, DLSS settings and upscaler choice step aside; the readout still works.\",\"dlss\":\"Kiln's DLSS settings step aside; the frame cap and the readout still work.\",\"upscaler\":\"Another upscaler tool's files are next to the game ({file})\",\"upscalerOnly\":\"Kiln's upscaler choice steps aside; the frame cap, DLSS settings and readout still work.\"},\"labels\":{\"fpsCap\":\"Frame cap\",\"dlssPreset\":\"DLSS render preset\",\"dlssScale\":\"DLSS render size\",\"dlssRenderScale\":\"DLSS render scale, every mode\",\"dlssQualityScale\":\"DLSS render scale, Quality\",\"dlssBalancedScale\":\"DLSS render scale, Balanced\",\"dlssPerformanceScale\":\"DLSS render scale, Performance\",\"dlssUltraPerformanceScale\":\"DLSS render scale, Ultra Performance\",\"readout\":\"Readout\"},\"upscaler\":{\"title\":\"Behind the game's FSR 3\",\"label\":\"Upscaler\",\"hint\":\"Choose FSR 3 as the upscaler in the game's graphics settings: Kiln runs your choice in its place. A change applies at once while FSR 3 runs in a world, else when a world loads. Chosen for the first time while the game runs, it applies from the next game start.\",\"choice\":{\"game\":\"The game's own FSR 3\",\"amd\":\"AMD FSR\",\"xess\":\"Intel XeSS\"},\"choiceHint\":{\"game\":\"Nothing changes: the game's FSR 3 runs as it does without Kiln.\",\"amd\":\"AMD FSR 3.1, or FSR 4 as an experimental choice on graphics cards and drivers that run it. Needs AMD's files (70 MB).\",\"xess\":\"Intel's XeSS, on Intel, AMD and NVIDIA cards. Needs Intel's file (74 MB).\"},\"amdLabel\":\"AMD FSR version\",\"amd\":{\"auto\":\"Auto (FSR 3.1 for now)\",\"fsr3\":\"FSR 3.1\",\"fsr4\":\"FSR 4 (experimental)\"},\"amdHint\":\"Auto runs FSR 3.1 until FSR 4 is confirmed in this game. FSR 4 (experimental) lets AMD choose: FSR 4 on graphics cards and drivers that run it, FSR 3.1 on the others; it may render dark scenes too dark.\",\"heading\":{\"now\":\"Now\",\"last\":\"Last game\",\"plan\":\"When you play\"},\"offer\":\"{name}'s files are not in this game yet. Until they are, the game's own FSR 3 runs.\",\"offerDamaged\":\"Some of {name}'s files are missing or changed. Until you download them again, the game's own FSR 3 runs.\",\"offerRunning\":\"Close the game to download them: the launcher does not change the game's files while it runs.\",\"running\":{\"off\":\"Kiln's upscaler is not in use behind the game's FSR 3 now.\",\"notFsr\":\"The game is not upscaling with FSR 3 right now: FSR 3 is not chosen in the game's graphics settings, or no world is loaded.\",\"gameFsr3\":\"The game's own FSR 3 runs.\",\"amdFsr4\":\"AMD FSR 4 runs in place of the game's FSR 3.\",\"amdFsr31\":\"AMD FSR 3.1 runs in place of the game's FSR 3.\",\"xess\":\"Intel XeSS runs in place of the game's FSR 3.\",\"unknown\":\"Kiln cannot tell what upscales behind the game's FSR 3: its parts are of different versions.\"},\"version\":\"Version {version}.\",\"amdFsr3Auto\":\"AMD's selection chose FSR 3.1: FSR 4 does not run on this graphics card or driver.\",\"changedNow\":\"You have chosen {upscaler} since that game started: it applies when the game next sets FSR up, or from its next start.\",\"restartNow\":\"Restart the game to use {upscaler}. Until then the game's own FSR 3 runs.\",\"detail\":\"Details: {detail}\",\"plan\":{\"amd\":\"With FSR 3 chosen in the game's graphics settings, Kiln runs AMD FSR 3.1 in its place. FSR 4 is available as an experimental choice.\",\"amdFsr3\":\"With FSR 3 chosen in the game's graphics settings, Kiln runs AMD FSR 3.1 in its place.\",\"amdFsr4\":\"With FSR 3 chosen in the game's graphics settings, Kiln runs AMD FSR in its place: FSR 4 where your graphics card and driver run it, else FSR 3.1. FSR 4 is experimental and may render dark scenes too dark.\",\"xess\":\"With FSR 3 chosen in the game's graphics settings, Kiln runs Intel XeSS in its place.\"},\"reason\":{\"restart\":\"{upscaler} runs from the next start of the game: Kiln sets it up when the game starts.\",\"filesMissing\":\"{upscaler}'s files are not downloaded yet, so the game's own FSR 3 runs.\",\"filesBad\":\"A file of {upscaler} is not the one Kiln expects (its size or SHA-256 differs), so Kiln does not load it. Download it again.\",\"loadFailed\":\"A file of {upscaler} did not load, so the game's own FSR 3 runs.\",\"device\":\"Your graphics card or driver lacks a Vulkan feature {upscaler} needs, so the game's own FSR 3 runs.\",\"createFailed\":\"{upscaler} could not be set up for this resolution, so the game's own FSR 3 runs.\",\"runtimeFailed\":\"{upscaler} failed while it upscaled. The game's own FSR 3 runs until you restart the game.\",\"fault\":\"{upscaler} or Kiln's code around it faulted. The game's own FSR 3 runs until you restart the game.\",\"aside\":\"Kiln steps aside for {tools}, so it does not change the game's FSR 3.\",\"noSignatures\":\"Kiln does not know this game build's FSR 3 yet, so the game's own FSR 3 runs. A Kiln update for this game build fixes that.\",\"inputs\":\"The game's images are not what {upscaler} takes, so the game's own FSR 3 runs.\",\"unavailable\":\"{upscaler} is not available in this build of Kiln or on this system, so the game's own FSR 3 runs.\",\"waiting\":\"{upscaler} is ready. It starts as soon as the game runs FSR 3: load a world with FSR 3 chosen in the game's graphics settings.\",\"noVulkan\":\"Kiln did not see the game set up its graphics device in this session. Restart the game for {upscaler}.\",\"unknown\":\"{upscaler} does not run (reason {number} from a newer Kiln), so the game's own FSR 3 runs.\"},\"tools\":{\"optiscaler\":\"OptiScaler\",\"streamline\":\"NVIDIA Streamline (a frame generation mod)\",\"vkfg\":\"a DLSS frame generation mod (EnshroudedVKFG.ini)\",\"dlsstweaks\":\"DLSSTweaks\",\"nvngx\":\"another DLSS loader (nvngx.dll next to the game)\",\"ngxPatched\":\"another tool that hooks DLSS\",\"overlayOff\":\"Kiln's overlay being off\",\"upscaler\":\"another upscaler tool (AMD's or Intel's upscaler files next to the game, or a changed FSR 3 pass)\",\"unknown\":\"another graphics tool\"}}},\"upscalers\":{\"title\":\"Upscaler files\",\"sub\":\"AMD's and Intel's upscaler files, for Kiln to run behind the game's FSR 3 option. Kiln downloads them only when you ask, into the game's kiln/vendor folder.\",\"purpose\":\"Kiln checks every file against the size and SHA-256 it pinned, and the game checks them again before it loads them. Remove deletes them again.\",\"unreadable\":\"The upscaler files could not be read\",\"starting\":\"Starting the download\",\"unavailable\":\"Not available in this build of Kiln.\",\"gameRunning\":\"Close the game to download or remove these files: the launcher does not change the game's files while it runs.\",\"damaged\":\"A file of it is missing or changed. Download it again.\",\"failedUnknown\":\"The download failed.\",\"failedTitle\":\"{name} was not downloaded\",\"size\":\"{size} from {host}.\",\"partial\":\"{done} of it arrived already: the download goes on from there.\",\"installed\":\"Version {version}, {size} on disk.\",\"update\":\"Version {installed} is downloaded. Version {version} is out: {size} from {host}.\",\"state\":{\"notDownloaded\":\"Not downloaded\",\"downloading\":\"Downloading\",\"downloaded\":\"Downloaded\",\"updateAvailable\":\"Update available\",\"unavailable\":\"Not available in this build\",\"failed\":\"Failed\"},\"action\":{\"download\":\"Download\",\"continue\":\"Continue download\",\"update\":\"Update\",\"tryAgain\":\"Try again\",\"downloadAgain\":\"Download again\"},\"actionLabel\":{\"download\":\"Download {name}\",\"continue\":\"Continue the download of {name}\",\"update\":\"Update {name}\",\"tryAgain\":\"Try the download of {name} again\",\"downloadAgain\":\"Download {name} again\",\"remove\":\"Remove {name}\",\"cancel\":\"Cancel the download of {name}\"},\"done\":\"{name} is downloaded\",\"doneDetail\":\"Kiln checked every file. They are in {folder} in the game folder.\",\"removeTitle\":\"Remove {name}?\",\"removeText\":\"Kiln deletes {folder} ({size}) from the game folder. You can download it again later.\",\"removed\":\"{name} is removed\",\"removedDetail\":\"{size} freed.\",\"removeFailed\":\"{name} was not removed\",\"dialog\":{\"title\":\"Download {name}\",\"what\":\"What\",\"size\":\"Size\",\"files\":{\"one\":\"{size}, {count} file\",\"other\":\"{size}, {count} files\"},\"from\":\"From\",\"fromText\":\"{host}: {vendor}'s own repository on GitHub, version {version}\",\"to\":\"Goes to\",\"toText\":\"{folder} in the game folder\",\"licenseLead\":\"{vendor} licenses these files under the {license}. Here is its text in full. Read it before you agree.\",\"licenseFrom\":\"Where this text is\",\"unreadable\":\"The licence text could not be read\",\"privacy\":\"Kiln asks {host} for these files and sends nothing else. {host} sees your IP address and a user agent that names Kiln and its version.\",\"agree\":\"I agree, download\"}},\"art\":{\"icons\":\"{count:N0} icons\",\"ui\":\"{count:N0} ui\",\"tiles\":\"{count:N0} tiles\",\"preparing\":\"preparing\",\"missing\":\"missing\",\"updating\":\"updating\",\"ready\":\"ready\",\"title\":\"Game art\",\"sub\":\"Made from your own game, on its own. Never shipped, never downloaded.\",\"unavailable\":\"Unavailable\",\"editor\":\"Save editor\",\"noClient\":\"No Enshrouded client found\",\"fromGame\":\"From your game, build {build}\",\"fromFolder\":\"Copied from a folder\",\"modItems\":\"Mod items: {items}\",\"modItemsIcons\":\"Mod items: {items} ({icons} own icons)\",\"noMods\":\"no mods\",\"admin\":\"Admin map\",\"reliefFromGame\":\"Relief from your game, build {build}\",\"fromLocalFolder\":\"Copied from a local folder\",\"markers\":\"{count:N0} markers from your game\",\"markersLocal\":\"markers from a local copy\",\"noMarkers\":\"no marker data\"},\"typeToConfirm\":\"Type a mod's name to remove it\",\"typeToConfirmHint\":\"Off: removing a mod is one click on Remove. Uninstalling, profiles, servers and backups always ask.\",\"sections\":{\"general\":{\"title\":\"General\",\"say\":\"The launcher speaks {language}. The fire, Steam Deck and the app menu entry are here too.\"},\"mods\":{\"title\":\"Mods & profiles\",\"say\":\"{name} is loaded in this game. How new mods behave, and the library with its data folder, are here too.\",\"sayNone\":\"No profile is loaded in this game: its folder decides which mods run.\"},\"game\":{\"title\":\"Game & Kiln\",\"say\":\"Kiln {version} is in {game}.\",\"sayNoKiln\":\"Kiln is not in {game} yet: install it on Runtime.\",\"sayNoGame\":\"No game found yet: Detect it, or add its folder.\"},\"graphics\":{\"title\":\"Graphics\",\"say\":\"Kiln's frame cap, DLSS settings and upscaler for {game}.\",\"sayServer\":\"Graphics settings are for the game, not a dedicated server.\"},\"online\":{\"title\":\"Updates & online\",\"say\":\"Everything here talks to the internet: launcher updates, Nexus Mods, the Community page.\"},\"advanced\":{\"title\":\"Advanced\",\"say\":\"Rarely needed. Change these when the launcher or the game misbehaves, or when support asks you to.\"}},\"summary\":{\"fireOn\":\"fire on\",\"fireOff\":\"fire off\",\"deck\":{\"auto\":\"Deck mode: auto\",\"deck\":\"Deck mode: always\",\"desktop\":\"Deck mode: off\"},\"loaded\":\"{name} loaded\",\"mods\":{\"one\":\"{count} mod\",\"other\":\"{count} mods\"},\"noProfile\":\"No profile loaded\",\"kiln\":\"Kiln {version}\",\"noKiln\":\"Kiln not installed\",\"noGame\":\"No game found\",\"noClient\":\"No game client\",\"upscaler\":{\"game\":\"Game's FSR 3\",\"amd\":\"AMD FSR\",\"xess\":\"Intel XeSS\"},\"cap\":\"cap {fps} fps\",\"noCap\":\"no frame cap\",\"nexusBuild\":\"Updates on Nexus Mods\",\"nexusOut\":\"Nexus: not signed in\",\"advanced\":\"Drawing, sandbox, kiln.json\"},\"groups\":{\"look\":\"Look\",\"computer\":\"On this computer\",\"adding\":\"Adding and removing mods\",\"kiln\":\"Kiln in the game\",\"kilnHint\":\"Also in the game's Kiln menu (Insert).\",\"graphics\":\"Kiln's graphics tools\",\"graphicsHint\":\"Also in the game's Kiln menu (Insert), under Graphics.\",\"graphicsMore\":\"The frame cap and the readout apply at once, DLSS settings when the game next sets up DLSS, the upscaler when the game next sets up FSR.\",\"updates\":\"Launcher updates\",\"community\":\"Discord\",\"sends\":\"What Kiln sends\",\"drawing\":\"Drawing the launcher\",\"drawingHint\":\"How WebKit draws this window on Linux. A change applies at the next start.\",\"protection\":\"Protection\",\"steam\":\"Steam folders\",\"kilnjson\":\"Kiln's file\",\"developer\":\"Developer\"},\"rows\":{\"languageLauncher\":\"The launcher's own texts. Applies at once.\",\"languageGame\":\"Kiln's own menus and messages in the game.\",\"fire\":\"The fire on Play. Off: a plain Play button.\",\"fireMore\":\"It draws with WebGL. Off is easier on weak GPUs.\",\"deck\":\"Auto opens Deck mode on a Steam Deck.\",\"desktop\":\"With Kiln's icon in the taskbar and dock.\",\"folderNotes\":\"Each note shows once, when it is new.\",\"folderNotesMore\":\"About folders Kiln left as they are, mod files it kept and your own folders. Off: only a folder that may still run a mod you switched off. Notes above your mods has them all.\",\"dailyUpdates\":\"Reads your mods' pages. Never installs anything.\",\"typeToConfirm\":\"Off: removing a mod is one click.\",\"keepVersions\":\"Per mod, for Roll back in its menu. 0 keeps none.\",\"keepVersionsMore\":\"At an update, Kiln keeps the mod's earlier version and the settings it had.\",\"games\":\"The installs the launcher knows.\",\"addFolder\":\"A copy of the game Steam does not list.\",\"defaultGame\":\"The install Play opens with.\",\"savesKeep\":\"Per game. The oldest goes first.\",\"amd\":\"Auto runs FSR 3.1 for now.\",\"upscalerGroup\":\"Choose FSR 3 as the upscaler in the game's graphics settings: Kiln runs your choice in its place.\",\"upscalers\":\"AMD's and Intel's files, downloaded only when you ask.\",\"channel\":\"Release, or the test builds (beta).\",\"updateState\":\"This launcher\",\"noGpu\":\"For a window that stays black.\",\"x11\":\"Much faster on a HiDPI screen, less sharp.\",\"forceDmabuf\":\"For NVIDIA cards. Off by default.\",\"sandbox\":\"Keep it on unless the window will not open.\"},\"tag\":{\"nextStart\":\"next start\",\"thisGame\":\"this game\"},\"more\":\"More\",\"less\":\"Less\",\"show\":\"Show\",\"hide\":\"Hide\",\"for\":\"For\",\"version\":\"Launcher {version}\",\"noClient\":\"No game found\",\"noClientHint\":\"Graphics are a game client's: add one under Game & Kiln.\",\"noClientServer\":\"No game client\",\"noClientServerHint\":\"Graphics settings apply to the game itself: a dedicated server draws no frames. Add the game under Game & Kiln.\",\"openGames\":\"Open Game & Kiln\",\"failed\":\"This section failed\",\"nav\":{\"label\":\"Settings sections\",\"groups\":\"Groups of {section}\",\"hits\":{\"one\":\"{section}: {count} match\",\"other\":\"{section}: {count} matches\"}},\"search\":{\"label\":\"Search\",\"key\":\"Ctrl F\",\"clear\":\"Clear the search\",\"title\":\"Search\",\"found\":{\"one\":\"{count} setting matches “{query}”.\",\"other\":\"{count} settings match “{query}”.\"},\"allIn\":\"All in {section}.\",\"change\":{\"one\":\"Change it here, or open it where it lives.\",\"other\":\"Change them here, or open one where it lives.\"},\"none\":\"Nothing matches “{query}”.\",\"foot\":\"Search reads every setting's name, its line of help and its More text, in the launcher's language. Enter opens the best match; Esc clears the search.\",\"inMore\":\"In More:\",\"openIn\":\"Open in {section}\"},\"kilnGame\":{\"labels\":{\"look\":\"Look\",\"theme\":\"Theme\",\"background\":\"Window background\",\"scale\":\"UI size\",\"chatHistory\":\"Chat history\",\"chatCompletion\":\"Chat completion\",\"outlines\":\"Mods' outlines\"}},\"kilnFile\":{\"labels\":{\"opacity\":\"Opacity of Kiln's whole UI\",\"overlay\":\"Kiln's overlay\",\"policy\":\"Game builds to hook\",\"logLevel\":\"kiln.log detail\",\"clr\":\".NET and managed mods\",\"plugins\":\"Kiln plugins\",\"nativePolicy\":\"When native mods load\",\"faultLimit\":\"Faults before a mod is switched off\",\"faultWindow\":\"Fault window\",\"stopTimeout\":\"Time a mod gets to stop\",\"testTimeout\":\"Time a mod test gets\",\"crashReports\":\"Crash reports\"}}}"),
	configEditor: {
		unavailable: "Configs unavailable",
		empty: "No configs yet",
		emptyHint: "Mods write their config and schema the first time they load.",
		files: "Config files",
		rawTip: "No schema snapshot: raw edit only",
		raw: "raw",
		schemaSub: "{version} · schema {ago}",
		filter: "Filter",
		view: "View",
		form: "Form",
		file: "File",
		cantRead: "Can't read it",
		saved: "Saved",
		revert: "Revert"
	},
	dataFolderSettings: {
		dialogFailed: "Could not open the folder dialog",
		checking: "Checking…",
		change: "Change",
		moving: "Moving the data folder",
		to: "To",
		restarting: "Restarting",
		moved: "Data folder moved",
		restartKiln: "Restart Kiln",
		restart: "Restart",
		nowIn: "Now in"
	},
	engineSettings: {
		invalidJson: "Enter a valid JSON value.",
		notSaved: "Not saved",
		unreadable: "The settings are not readable",
		closeGame: "Close the game before editing engine settings.",
		activateProfile: "Activate this profile in the selected game to edit ShroudForge settings.",
		ownFolder: "This is your own folder, not a copy Kiln placed: Kiln changes only the setting you change here.",
		none: "No settings.",
		titleOf: "{name} settings",
		title: "Settings",
		modalHint: "{kind} mod. Changes are written right away; the game reads them at its next start."
	},
	engineStatus: {
		steppedAside: "stepped aside",
		waitingAbi: "waiting for Ember's ABI",
		running: "running",
		idle: "idle",
		unreadable: "The engine state files are not readable",
		none: "No other engine has run yet.",
		noneHint: "Kiln's engine plugins write their state at each start.",
		plugins: "Engine plugins",
		ownLoader: "own loader installed: {file}",
		selfHostedGone: "Last start: self-hosted (loader no longer detected)",
		hostRecognized: "Host recognized (not verified)",
		requiresXhl: "Requires XHL-Core",
		unreadableShort: "Unreadable",
		reportAvailable: "Runtime report available",
		notRun: "Not run yet",
		mods: {
			one: "{count} mod",
			other: "{count} mods"
		},
		effects: "ShroudForge mod effects",
		sfReport: "ShroudForge report: {state}",
		noEffects: "No per-mod effects reported yet.",
		writeConfirmed: "Memory write confirmed; not proof of the gameplay effect.",
		ownReports: "These are the mods' own reports, not proof of the gameplay effect.",
		backupUnreadable: "The game data backup is not readable",
		listUnreadable: "Your mod list is not readable",
		lastStart: "On the last start: {reason}.",
		lastStartNoEml: "On the last start without EML mods: {reason}.",
		emlNote: "EML archive note",
		differsTitle: "Shared archive backup differs",
		differs: {
			one: "{names} differs from the shared backup; this does not identify which loader wrote it.",
			other: "{names} differ from the shared backup; this does not identify which loader wrote it."
		},
		staleBackup: "The backup is of an earlier game build: the game was updated since it was made. Kiln does not compare the game data with it and restores nothing from it; the next start with an EML mod makes a new backup.",
		nothingToDo: "Nothing needs doing.",
		removeHow: "To remove EML changes, disable EML mods and start the game through Kiln: its archive writer checks ownership and restores only output it can prove it wrote (earlier Kiln versions' too). When it cannot, it leaves the data alone and says why here; then enable any EML mod for one start and disable it again, or use Steam's \"Verify integrity of game files\".",
		othersKept: "Other loaders' data is kept; the launcher never copies shared backups directly."
	},
	folderNotes: {
		nothing: "Nothing to note: the last time Kiln wrote your mods to the game, it had nothing to say about their folders.",
		retained: "Left in the game folder",
		keptFiles: "Mod files kept",
		warnings: "Notes on your mods",
		buttonTip: "What Kiln said about your mod folders the last time it wrote your mods to the game",
		button: "Notes ({count})",
		title: "Notes about your mod folders",
		intro: "From the last time Kiln wrote your mods to the game. Kiln shows each note once when it is new; Settings, Launcher can turn that off."
	},
	gameArt: {
		editorFailed: "Game art could not be prepared",
		mapFailed: "Map art could not be prepared"
	},
	librarySettings: {
		title: "Library",
		sub: "Where Kiln keeps every mod's files, by version.",
		cleanUp: "Clean up",
		unreadable: "The library is not readable",
		mods: "Mods",
		modsVersions: "{count} ({versions} versions)",
		modsSeveral: "{count} ({versions} versions; {several} with more than one: their menu on Play picks one)",
		onDisk: "On disk",
		kept: "Kept for a roll back",
		keptCount: {
			one: "{count} earlier version, {size}",
			other: "{count} earlier versions, {size}"
		},
		dataFolder: "Data folder",
		keptVersions: "Kept versions",
		keptSettings: {
			one: "{count} settings file",
			other: "{count} settings files"
		},
		replacedBy: "replaced by {name}",
		removeVersion: "Remove {name} {version}",
		hint: "Clean up removes the versions no profile and no game uses, not the ones kept for a roll back. Removing one mod: its menu on Play. Rolling back: the mod's menu on Play.",
		removeVersionAsk: "Remove {name} {version}?",
		removeVersionMessage: "This earlier version and the settings kept with it leave the Library: you can no longer roll back to it.",
		removedVersion: "Removed {name} {version}",
		removeUnusedAsk: "Remove {count} unused?",
		removeUnusedMessage: "No profile and no game uses them. Profiles and saves stay as they are.",
		removedCount: "Removed {count}",
		removedDetail: {
			one: "{count} version left the library.",
			other: "{count} versions left the library."
		},
		nothingToClean: "Nothing to clean",
		nothingToCleanText: "Every version is in a profile, or in the profile file of a game."
	},
	nexusSettings: {
		notSignedInFailed: "Not signed in",
		signedInToast: "Signed in to Nexus Mods",
		signedOutToast: "Signed out of Nexus Mods",
		notChanged: "Not changed",
		title: "Nexus Mods",
		sub: "Sign in through your browser.",
		signedInAs: "Signed in as {name}",
		signedIn: "Signed in",
		premium: "Premium",
		free: "Free",
		notSignedIn: "Not signed in.",
		notInBuild: "Nexus Mods sign-in is not available in this build.",
		reconnect: "Reconnect",
		signIn: "Sign in to Nexus Mods",
		signOut: "Sign out",
		handler: "Mod Manager Download links",
		handlerUnsupported: "Not available on this system.",
		handlerHint: "Enshrouded's links from Nexus Mods open in Kiln.",
		handlerPrevious: "Other games' links go to {program}, which gets them all back when this is off.",
		handlerAlone: "Other games' links are left alone.",
		handlerTaken: "Another program has taken the links since: turn this off and on again."
	},
	ownFolders: {
		already: "Kiln manages this folder already",
		now: "Kiln manages this folder now",
		nowPath: "{path}.",
		nowPathRenamed: "{path} (it was {old}).",
		nowSettingsKept: "Your setting values stay as they are.",
		nowSwitches: "Kiln switches and updates it with the mod from now on.",
		activateFailed: "Kiln could not write your mods to the game again",
		adoptFailed: "Kiln did not take the folder over",
		rowHint: "{id} {version}: your own folder. Kiln neither switches, updates nor removes it.",
		closeGame: "Close the game first",
		canTakeOver: "Its files are the mod's as your Library has them: Kiln can take it over",
		manage: "Let Kiln manage this folder",
		confirmTitle: "Let Kiln manage this folder?",
		confirm: "Let Kiln manage it",
		confirmText: "Its files are the files of {id} {version} in your Library. Kiln will update, switch and remove this folder with the mod from now on.",
		switches: "It switches the mod in this folder on and off as your profile says.",
		replaces: "It replaces the folder when the mod updates, and carries your setting values over.",
		removes: "It removes the folder when it takes its mods out of the game, unless the folder then holds setting values of yours.",
		renames: "It renames the folder to {name}, the name its copies of this mod have. ShroudForge finds the mod by the id inside it, not by the folder's name.",
		nothingNow: "Nothing in the folder is changed now, except one small file Kiln adds to know the folder again (.kiln-library.json). Kiln then writes your mods to the game again.",
		section: "Your own mod folders",
		sectionHint: "You put these into the game yourself, so Kiln leaves them as they are. When a folder's files are exactly the mod's, Kiln can manage it like a copy it placed. It never does that by itself."
	},
	profilesSettings: {
		noProfile: "No profile",
		loadedToast: "{name} is loaded",
		noneLoadedToast: "No profile is loaded",
		loadedDetail: "The main screen's switches show it. Steam's Play button uses it too.",
		loadedDetailDirect: "The main screen's switches show it.",
		noneLoadedDetail: "The mods in the game folder load as they are.",
		findGame: "Find the game on Play first",
		installKiln: "Install Kiln first",
		closeGame: "Close the game first",
		title: "Profiles",
		subLoaded: "A profile is a saved set of mods and their switches. {name} is loaded: the main screen shows it.",
		sub: "A profile is a saved set of mods and their switches.",
		loadFirst: "Load a profile first",
		saveAs: "Save current as…",
		createEmpty: "Create empty local profile…",
		createEmptyTitle: "Create empty local profile",
		createEmptyHint: "No mods or settings are copied. This only saves a profile in this launcher's data folder; it does not load it into the game.",
		createdLocal: "Created local profile {name}",
		createdLocalDetail: "Saved locally only. The game's loaded profile has not changed.",
		unreadable: "Profiles are not readable",
		saved: "Saved profiles",
		line: {
			one: "{count} mod · changed {ago}",
			other: "{count} mods · changed {ago}"
		},
		lineServer: {
			one: "{count} mod · for {server} · changed {ago}",
			other: "{count} mods · for {server} · changed {ago}"
		},
		loaded: "Loaded",
		load: "Load",
		more: "More for {name}",
		rename: "Rename",
		folders: "Folders and notes",
		noProfileSub: "The game folder decides: the mods in it load as they are.",
		savedAs: "Saved as {name}",
		staysLoaded: "{name} stays loaded.",
		loadedStaysLoaded: "The loaded profile stays loaded.",
		loadIt: "Load it",
		deleteAsk: "Delete {name}?",
		deleteMessage: "Its configs go to backups. The mods stay.",
		deleted: "Profile deleted",
		saveAsTitle: "Save current as",
		name: "Name",
		copyOf: "A copy of {name}: its mods, switches and configs.",
		copyName: "{name} copy",
		renamed: "Renamed to {name}",
		renameTitle: "Rename {name}"
	},
	restoreDataOffer: {
		title: "Restore the game's data",
		moved: "Kiln moved these files to {folder}. Nothing was deleted.",
		freshBackup: "The next start through Kiln makes a fresh backup from the game data as it is now.",
		undo: "Undo move",
		offer: "Files of another mod loader are in the game folder, and Kiln's EML mods cannot start beside them. Kiln can move them aside.",
		moveAside: "Move files aside",
		explain: "Another mod loader left archive files and backups in the game folder that Kiln cannot prove it wrote. First verify the game files in Steam (the game's Properties, Installed Files, \"Verify integrity of game files\"). Then Kiln moves the files below into a folder under kiln/eml/displaced. Nothing is deleted, and you can undo the move.",
		repaired: "If Steam repaired files, Kiln asks you to start the game once more before it moves anything: it reads the list of leftover files at a start.",
		verified: "I verified the game files in Steam",
		mb: "{size} MB",
		kb: "{size} KB"
	},
	saveBackups: {
		reason: {
			launch: "before launch",
			modpack: "modpack",
			manual: "manual",
			restore: "before restore"
		},
		title: "Backups",
		when: "When",
		why: "Why",
		profile: "Profile",
		size: "Size",
		closeGame: "Close the game first",
		restore: "Restore",
		more: "More",
		backUpNow: "Back up now",
		running: "The game is running. Restore waits until it closes.",
		unavailable: "Backups unavailable",
		label: "Save backups",
		none: "No backups",
		noneHint: "Kiln backs up before a launch with different mods.",
		restoreAsk: "Restore this backup?",
		restoreMessage: "Your current saves are backed up first. Steam Cloud may then ask which copy to keep: pick {local}.",
		local: "local",
		restored: "Restored",
		deleteAsk: "Delete this backup?",
		backedUp: "Backed up",
		backUpTitle: "Back up saves",
		backUp: "Back up",
		note: "Note",
		optional: "Optional.",
		notePlaceholder: "before the boss"
	},
	sfSelfHostedNotice: {
		title: "ShroudForge's own loader is installed",
		manage: "Let Kiln manage ShroudForge"
	},
	emlRNotice: {
		title: "Kiln found EML-R's loader ({file})",
		text: "EML-R loads the mods on its own list when it starts, independently of Kiln and its profile switches.",
		interpreter: "Kiln's ShroudForge interpreter stands aside while EML-R's loader is in the game folder because EML-R may run those mods itself."
	},
	shroudlinkNotice: {
		title: "Shroudlink client addon",
		switchOn: "Switch addon on"
	},
	reportContents: {
		title: "What's in this report",
		intro: "Every file in the report, and what Kiln replaced in it before anything leaves your PC. Kiln's own logs always stay in: support needs them to see what went wrong. Other mods' logs you can leave out.",
		total: {
			one: "{count} file, {size} before compression",
			other: "{count} files, {size} before compression"
		},
		leftOutCount: {
			one: "{count} file left out",
			other: "{count} files left out"
		},
		failed: "The report could not be read",
		view: "View summary.txt",
		hideSummary: "Hide summary.txt",
		viewTitle: "summary.txt",
		save: "Save",
		leaveOut: "Leave out",
		leaveOutAll: "Leave out all other mods' logs",
		leftOut: "Left out",
		leftOutHint: "Left out: not in the zip.",
		included: "In",
		include: "Put back",
		replaced: "Replaced: {list}",
		item: "{what} ({times})",
		times: {
			one: "{count} time",
			other: "{count} times"
		},
		sep: ", ",
		nothing: "Nothing personal found in it.",
		group: {
			kiln: "Kiln's logs",
			mods: "Other mods' logs",
			settings: "Settings",
			listings: "Folder listings"
		},
		groupHint: {
			kiln: "Support needs these to see what went wrong, so they always stay in.",
			mods: "Written by other mods and tools. Kiln cleans them the same way, but it can't know everything another mod writes, so you can leave them out.",
			settings: "Kiln's own state and your profile: which mods are on and what the last start did. Passwords and tokens in them are replaced.",
			listings: "Only the names, sizes and dates of files and folders. Nothing inside them."
		},
		token: {
			home: "your home folder",
			user: "your user name",
			host: "your PC's name",
			"steam-name": "Steam names",
			steamid: "Steam IDs",
			player: "player names",
			ip: "IP addresses",
			mac: "network card addresses",
			email: "email addresses",
			redacted: "passwords, tokens and keys"
		},
		about: {
			summary: "The summary: what Kiln found, your system, the game and your profile",
			findings: "What Kiln found, for the support tools",
			"kiln-log": "Kiln's main log from the last game start",
			"proxy-log": "How Kiln was loaded into the game",
			"eml-log": "Kiln's log for EML mods",
			"tick-stats": "How long each world step took, minute by minute",
			"greeneye-bridge": "Kiln's log for Greeneye mods",
			crash: "The newest crash report Kiln wrote",
			"launcher-log": "The launcher's own log",
			"game-start": "What the game printed when the launcher started it",
			"kiln-other": "One of Kiln's logs",
			state: "What Kiln saw at the last start",
			profile: "Your profile: which mods are on",
			chain: "Which other loaders start with the game",
			"kiln-json": "Kiln's settings in the game folder",
			receipt: "Which Kiln files are installed, with their checksums",
			"game-folder": "The names in the game folder",
			"kiln-folder": "The names in Kiln's folder",
			"mod-folders": "The names in each mods folder",
			listing: "A list of file names",
			"xhl-core": "XHL-Core's own log",
			greeneye: "Greeneye Loader's or a Greeneye mod's own log",
			"building-tools": "Building Tools' own log",
			shroudlink: "ShroudLink's own log",
			"auto-loot": "Auto Loot Plus' own log",
			"optiscaler-settings": "OptiScaler's settings",
			"optiscaler-log": "OptiScaler's own log",
			"mod-log": "Another mod's own log"
		},
		never: {
			title: "Never in a report",
			saves: "Your saves and worlds",
			settings: "Mod settings and Kiln's config folder (they can hold server passwords)",
			admin: "Kiln.Admin's token and address",
			mods: "What's inside your mods (only file and folder names, sizes and dates are listed, plus ShroudForge manifest IDs and versions in findings)",
			steam: "Steam's own files"
		}
	},
	supportReport: {
		dialogTitle: "Save a support report",
		saved: "Report saved",
		savedFile: "{file}, {size}.",
		savedPath: "Saved to {path} ({size}).",
		saveDownloads: "Save to Downloads",
		problems: {
			one: "It found {count} problem.",
			other: "It found {count} problems."
		},
		attach: "Attach it to your post.",
		openFolder: "Open folder",
		notSaved: "The report was not saved",
		noClipboard: "Clipboard unavailable",
		copied: "Summary copied",
		copiedDetail: "Paste it into your post. The zip has the details.",
		copyFailed: "Could not copy the summary",
		title: "Support report",
		hint: "Mods don't load? Save one zip with Kiln's logs, its state files, the profile and a list of the game folder. Your name, paths and server addresses are replaced; saves and mod settings stay out.",
		save: "Save report...",
		copy: "Copy summary"
	},
	updateSettings: {
		checking: "Checking…",
		upToDate: "Up to date",
		available: "{version} available",
		skipped: "Skipped {version}",
		failed: "Check failed",
		updating: "Updating…",
		ready: "Ready to restart",
		notChecked: "Not checked yet",
		off: "Off",
		openFailed: "Could not open the page",
		title: "Updates",
		downloadPage: "the download page",
		newVersions: "New versions: {link}",
		nexusBeta: "Test builds (beta): a launcher from {link} can switch to them.",
		undoFailed: "Undo failed",
		auto: "Check automatically",
		autoHint: "Sends no personal data.",
		channel: "Channel",
		checkNow: "Check now",
		testBuild: "Test build {beta} of {current}.",
		switchBack: "Switch back to the release",
		followBeta: "Following the beta: the next test build comes as an update.",
		checkingNow: "Checking for updates…",
		upToDateLine: "Kiln is up to date ({version})",
		availableLine: "Kiln {version} is available",
		failedLine: "Check failed: {reason}",
		checkedAt: "checked {time}",
		noAnswer: "no update server answered"
	},
	nav: {
		play: "Play",
		catalog: "Catalog",
		runtime: "Runtime",
		logs: "Logs",
		conflicts: "Conflicts",
		saves: "Saves",
		editor: "Editor",
		servers: "Servers",
		settings: "Settings",
		about: "About",
		mods: "Mods"
	},
	shell: /* @__PURE__ */ JSON.parse("{\"serverNav\":{\"groupRun\":\"Run\",\"overview\":\"Overview\",\"logs\":\"Logs\",\"tickStats\":\"Tick stats\",\"groupWorld\":\"World\",\"players\":\"Players\",\"map\":\"Map\",\"zones\":\"Zones\",\"spawn\":\"Spawn\",\"creatures\":\"Creatures\",\"combat\":\"Combat\",\"world\":\"World\",\"groupData\":\"Data\",\"items\":\"Items\",\"gameData\":\"Game data\",\"content\":\"Content\",\"permissions\":\"Permissions\",\"groupFiles\":\"Files\",\"settings\":\"Settings\",\"configs\":\"Configs\",\"mods\":\"Mods\",\"backups\":\"Backups\",\"saves\":\"Saves\",\"modpack\":\"Modpack\",\"crashes\":\"Crashes\",\"console\":\"Console\",\"worldRules\":\"World rules\"},\"notices\":{\"keptTitle\":\"Some mod folders were kept\",\"keptDetail\":\"{folders} Kiln left these folders as they are. With the game closed, move a folder out of the game folder to stop a mod that is off; your files have not been deleted.\",\"settingsKept\":\"Mod settings kept\",\"modNotes\":\"Notes on your mods\",\"listChanges\":\"Changes in your mod list\",\"runtimeKeptTitle\":\"Changed runtime files kept\",\"runtimeKeptDetail\":\"These files changed outside Kiln and were left in the game folder: {files}. With the game closed, review them before removing or replacing them.\",\"backedUpTitle\":\"Changed runtime files backed up\",\"backedUpDetailIn\":\"The install replaced these changed files: {files}. Their previous copies were kept in {backup}.\",\"backedUpDetail\":\"The install replaced these changed files: {files}. Their previous copies were kept in the game's kiln/backups folder.\",\"filesKept\":\"Some files were kept\",\"installNotes\":\"Install notes\"},\"dataFolderText\":{\"listAnd\":\"{list} and {last}\",\"envWhy\":\"Set by KILN_DATA_DIR.\",\"cannotMove\":\"This launcher cannot move its data folder.\",\"files.one\":\"{count:N0} file\",\"files.other\":\"{count:N0} files\",\"refuseTitle\":\"Kiln cannot use this folder\",\"folder\":\"Folder\",\"refuseReason\":\"This folder cannot hold the data folder.\",\"chooseAnother\":\"Choose another\",\"useTitle\":\"Use this data folder?\",\"useLine\":\"It already holds Kiln data. The current folder stays where it is.\",\"useIt\":\"Use it\",\"intoNewFolder\":\"That folder has other files in it, so the data goes into a new folder inside it.\",\"copyMode\":\"From there, mods are copied into the game folder of {games}.\",\"stays\":\"These stay in the old folder too, Kiln copies them and does not remove them: {list}.\",\"moveTitle\":\"Move the data folder?\",\"to\":\"To\",\"moves\":\"Moves\",\"movesValue.one\":\"{size}, {count:N0} file\",\"movesValue.other\":\"{size}, {count:N0} files\",\"freeThere\":\"Free there\",\"move\":\"Move\",\"removingOldCopy\":\"Removing the old copy. Cancel leaves the rest for the next start.\",\"restNextStart\":\"The data has moved. The rest of the old copy goes at the next start.\",\"moveCanceled\":\"Move canceled\",\"moveFailed\":\"Move failed\",\"staysIn\":\"Nothing changed. The data stays in\",\"undoneLine\":\"The last move did not finish. Kiln uses {folder} as before.\",\"undoneUnfinished\":\"Kiln stopped removing the unfinished copy: the rest goes at the next start.\",\"undoneKept\":\"Kiln could not remove these from the unfinished copy:\",\"undoneTitle\":\"Move not finished\",\"dataFolder\":\"Data folder\",\"unfinishedCopy\":\"Unfinished copy\",\"openUnfinished\":\"Open unfinished copy\",\"unclearTitle\":\"Kiln did not finish a move\",\"oldFolder\":\"Old folder\",\"newFolder\":\"New folder\",\"openOld\":\"Open old folder\",\"removing\":\"Kiln is removing the old copy.\",\"partStill\":\"Part of the old copy is still there: Kiln removes the rest at the next start.\",\"unknownLeft.one\":\"Kiln did not remove this, it does not know it:\",\"unknownLeft.other\":\"Kiln did not remove these, it does not know them:\",\"couldNotRemove.one\":\"Kiln could not remove this:\",\"couldNotRemove.other\":\"Kiln could not remove these:\",\"stayedOld.one\":\"This stayed in the old folder:\",\"stayedOld.other\":\"These stayed in the old folder:\",\"movedTitle\":\"Data folder moved\",\"nowIn\":\"Now in\",\"undoingTitle\":\"Removing an unfinished copy\",\"undoingCancelLine\":\"Cancel leaves the rest for the next start; Kiln opens as usual.\",\"elsewhereTitle\":\"Kiln is moving its data in another window\",\"otherFolderProfile\":\"Another data folder's profile is loaded in this game\",\"otherFolderTitleRoot\":\"Profile \\\"{id}\\\" of the data folder {root}. Loading a profile here replaces it.\",\"otherFolderTitle\":\"Profile \\\"{id}\\\" of another data folder. Loading a profile here replaces it.\",\"brokenTitle\":\"Kiln cannot open its data folder\"},\"dataFolderGate\":{\"starting\":\"Starting…\",\"restarting\":\"Restarting\",\"quit\":\"Quit\",\"startFresh\":\"Start fresh\",\"startFreshTip\":\"An empty data folder in {path}\",\"chooseFolder\":\"Choose folder\"},\"dataFolder\":{\"restartToUse\":\"Restart Kiln to use the new folder.\",\"restartFailed\":\"{error} Restart Kiln to use the new folder.\",\"checkFailed\":\"Could not check this folder\",\"cancelFailed\":\"Could not cancel the move\"},\"gameState\":{\"vanilla\":\"Vanilla\",\"startedWithoutKiln\":\"Started without Kiln\",\"noKiln\":\"No Kiln\",\"noKilnDetail\":\"The game runs without Kiln: Steam did not load it\",\"noKilnDetailDirect\":\"The game runs without Kiln: it did not load Kiln's proxy this start\",\"checking\":\"Checking\",\"noGame\":\"No game\",\"updating\":\"Updating\",\"steamUpdating\":\"Steam is updating the game\",\"stopped\":\"Stopped\",\"starting\":\"Starting\",\"waitingBoot\":\"Waiting for Kiln to boot\",\"running\":\"Running\",\"pid\":\"pid {pid}\",\"safeIdle\":\"Safe idle\",\"whatSafeIdle\":\"safe idle\",\"whatDropIns\":\"drop-ins\",\"kiln\":\"Kiln {version}\",\"kilnWhat\":\"Kiln {version} · {what}\"},\"play\":{\"applyingProfile\":\"Applying profile…\",\"installingKiln\":\"Installing Kiln…\",\"removingKiln\":\"Removing Kiln…\",\"applyingAria\":\"{what} ({title})\",\"stoppedStart\":\"Kiln stopped the start\",\"waitingForKiln\":\"Waiting for Kiln\",\"stopWaiting\":\"Stop waiting\",\"stopWaitingTip\":\"Steam has not started the game. Stop waiting and show Play again, for example after you cancelled the launch in Steam. Nothing is closed.\",\"noWord\":\"No word from Kiln\",\"hookedNothing\":\"Kiln hooked nothing\",\"kilnStopped\":\"Kiln stopped\",\"couldNotGoOn\":\"Kiln could not go on\",\"startingVanilla\":\"Starting vanilla\",\"startingNoMods\":\"Starting without mods\",\"savesBackedUp\":\"Saves backed up first.\",\"throughSteam\":\"Through Steam.\",\"kilnInstalled\":\"Kiln {version} installed\",\"switchBackDetail\":\"This game had Kiln {from} from the beta; the release's goes with this launcher.\",\"switchBackFailed\":\"Kiln {to} could not be installed: the game starts with Kiln {from} from the beta\",\"directly\":\"Started directly.\"},\"startProgress\":{\"quietMods\":\"The game is waiting at its menu: Kiln loads the mods when the game asks for them (on some PCs only once a world loads).\",\"quietGame\":\"Kiln has loaded the mods. The game is still loading, or it is at its menu and Kiln did not see the menu appear; this goes away once you load a world.\",\"quietLast\":\"Kiln has said nothing for {secs} s (last: {last}). A step can be slow on a busy PC; if the game does not move, close it and save a support report on the Logs page.\",\"quiet\":\"Kiln has said nothing for {secs} s. A step can be slow on a busy PC; if the game does not move, close it and save a support report on the Logs page.\",\"fewSeconds\":\"a few seconds\",\"aboutMinSec\":\"about {min} min {sec} s\",\"aboutMin\":\"about {min} min\",\"aboutSec\":\"about {sec} s\",\"sec\":\"{sec} s\",\"minSec\":\"{min} min {sec} s\",\"min\":\"{min} min\",\"basisSame.one\":\"From your last start with these mods\",\"basisSame.other\":\"From your last {count} starts with these mods\",\"basisOther.one\":\"From your last start of this game, with other mods or another Kiln\",\"basisOther.other\":\"From your last {count} starts of this game, with other mods or another Kiln\",\"stepDetailCount\":\"{label}: {detail} ({done} of {total})\",\"stepDetail\":\"{label}: {detail}\",\"stepCount\":\"{label} ({done} of {total})\",\"stepOf\":\"Step {n} of {total}\",\"stepOfShort\":\"step {n} of {total}\",\"steamSlow\":\"Steam is still preparing the game: this can take minutes the first time.\",\"steamSlowShort\":\"Steam is preparing\",\"firstStart\":\"First start with these mods on this PC: Kiln is timing it. {stepOf}.\",\"thisStep\":\"This step\",\"slow\":\"{step}: taking longer than usual (usually {usual}).\",\"slowThen\":\"{step}: taking longer than usual (usually {usual}). Then {about}.\",\"slowShort\":\"longer than usual\",\"left\":\"{about} left.\",\"leftShort\":\"{about} left\",\"valueFirst\":\"{stepOf}: {line}\",\"value\":\"{pct} percent, {short}: {line}\",\"done\":\"done\",\"usually\":\"usually {took}\",\"now\":\"now\",\"notNeeded\":\"not needed\",\"directSlow\":\"The game is still starting: a first direct start makes its prefix, which can take minutes.\",\"directSlowShort\":\"Preparing the prefix\",\"sfStartup\":\"Preparing ShroudForge mods for Kiln's interpreter.\",\"sfStartupStep\":\"Preparing ShroudForge mods for Kiln's interpreter: {step} ({n} of {total}).\",\"sfStartupStepOnly\":\"Preparing ShroudForge mods for Kiln's interpreter: {step}.\",\"sfStepChecking\":\"checking the game files\",\"sfStepMods\":\"running the mods\",\"sfStepVerify\":\"checking the prepared game data\",\"sfStepWrite\":\"writing the game data\",\"sfStepSave\":\"saving the result\"},\"appShell\":{\"headStart\":\"{progress} · {step}\",\"pct\":\"{pct}%\",\"labelTime\":\"{label} {time}\",\"logoAria\":\"Kiln: Play\",\"logoTitle\":\"Kiln Launcher {version}\",\"navAria\":\"Main\",\"browserMock\":\"Browser mock\",\"mockFor\":\"Mock data for: {list}\",\"mock\":\"Mock\",\"lineLeft\":\"{line}. {left}\",\"looking\":\"Looking\",\"lookingSub\":\"for Enshrouded\",\"lookingAria\":\"Looking for Enshrouded in your Steam libraries\",\"addGame\":\"Add game\",\"addGameSub\":\"Kiln looks in every Steam library\",\"addGameAria\":\"Add game: find Enshrouded in your Steam libraries\",\"checkingGame\":\"Checking the game\",\"installing\":\"Installing\",\"installSub\":\"Kiln goes into the game folder\",\"installingAria\":\"Installing Kiln\",\"installKiln\":\"Install Kiln\",\"startingAria\":\"Starting: {line}. {left}\",\"labelFor\":\"{label} for {time}\",\"playName\":\"Play {name}\",\"docTitle\":\"{page} · Kiln\"},\"bootSnapshot\":{\"notMade\":\"The game data for OnBoot mods was not made ({reason}). The game starts without it: a mod that reads the game's data in OnBoot is told it is unavailable.\",\"noReason\":\"no reason given\",\"empty\":\"The game data for OnBoot mods has no {kinds} (the game's archive could not give them): an OnBoot read of them finds nothing.\"},\"cook\":{\"reasonNotCooked\":\"it was not cooked for this game yet\",\"reasonModChanged\":\"the mod changed since its last cook\",\"reasonGameChanged\":\"the game was updated since its last cook\",\"reasonLastFailed\":\"its last cook failed\",\"reasonFilesGone\":\"its cooked files are gone or were changed\",\"reasonAsked\":\"you asked\",\"notCookedText\":\"{name}:\\n{error}\",\"noWord\":\"the cook failed without a word\",\"oneCooked\":\"{name} cooked\",\"manyCooked\":\"{count} mods cooked\",\"cookedDetail\":\"Their models, animations and sounds are made for this game.\",\"oneNotCooked\":\"{name} was not cooked\",\"manyNotCooked\":\"{count} mods were not cooked\",\"failedRestOthers.one\":\"{count} other mod was cooked. The game starts all the same, and a mod that could not be cooked keeps running on the files it has: those of its last cook, or none if it was never cooked here. \\\"Cook again\\\" in the mod's menu tries again.\",\"failedRestOthers.other\":\"{count} other mods were cooked. The game starts all the same, and a mod that could not be cooked keeps running on the files it has: those of its last cook, or none if it was never cooked here. \\\"Cook again\\\" in the mod's menu tries again.\",\"failedRest\":\"The game starts all the same, and a mod that could not be cooked keeps running on the files it has: those of its last cook, or none if it was never cooked here. \\\"Cook again\\\" in the mod's menu tries again.\",\"cookingOne\":\"Cooking {name}\",\"cookingMany\":\"Cooking {count} mods\",\"cookingDetail\":\"Making their models, animations and sounds from your game, then the game starts.\",\"bootData\":\"Game data for OnBoot mods\",\"notCookedTitle\":\"Mods were not cooked\",\"startsAnyway\":\"{error} The game starts all the same.\",\"bootDataNotMade\":\"Game data for OnBoot mods was not made\"},\"catalogInstall\":{\"findGameFirst\":\"Find Enshrouded on Play first.\",\"installKilnFirst\":\"Install Kiln first.\",\"removed\":\"Removed {name}\",\"undoFailed\":\"Undo failed\",\"notAdded\":\"Not added\",\"needs\":\"Needs {list}.\",\"nextStart\":\"Applies at the next start.\",\"noProfile\":\"No profile is loaded: it is in your list, off.\",\"get\":\"Get {name}\",\"updated\":\"Updated {name}\",\"added\":\"Added {name}\",\"onAlready\":\"{name} is on already\",\"inListOff\":\"{name} is in your list, off\",\"downloadWaiting\":\"A Nexus Mods download is waiting\"},\"channelSwitch\":{\"openPageFailed\":\"Could not open the page\",\"nothingToSwitch\":\"Nothing to switch to\",\"switchFailed\":\"Switch failed\",\"restartFailed\":\"Restart failed\",\"later\":\"Later\",\"restartNow\":\"Restart now\",\"openDownloadPage\":\"Open the download page\",\"readyTitle\":\"Ready: {build}\",\"noCompleteCopy\":\"No complete copy from before the beta: {list}\",\"readyLine\":\"{state} {restart} {then}\",\"releaseInPlace\":\"The release is in place.\",\"testInPlace\":\"The test build is in place.\",\"verified\":\"The build is verified and ready to install.\",\"restartToRun\":\"Restart now to run it.\",\"restartRestore\":\"Restart now to run it; your profiles and settings from before the beta go back first.\",\"installTestKiln\":\"Install the test build’s Kiln on the Runtime page to use it in your game.\",\"thisIsTest\":\"This is test build {beta} of {current}.\",\"notInPlace\":\"{reason} The download page has the build; the launcher follows its channel from now on.\",\"betaTitle\":\"Test build {beta} of {current}. Click to switch back to the release.\",\"betaAria\":\"Beta {beta}: switch back to the release\",\"beta\":\"Beta {beta}\"},\"drop\":{\"undoKept\":\"Undo kept later changes\",\"undoKeptDetail.one\":\"{count} mod was changed since this add.\",\"undoKeptDetail.other\":\"{count} mods were changed since this add.\",\"undone\":\"Undone\",\"updatedFromTo\":\"Updated {name}: {from} → {to}\",\"addedAndUpdated\":\"Added {added}, updated {updated}\",\"noGame\":\"No game yet\",\"noGameModpack\":\"Find Enshrouded on Play before installing a modpack.\",\"headingAddedOff\":\"Added, switched off\",\"addedOff\":\"Added {names}, switched off\",\"switchOnHint\":\"Switch it on in the list on Play.\",\"nothingToImport\":\"Nothing to import\",\"importFailed\":\"Import failed\",\"loaded\":\"{name} is loaded\",\"loadedProfile\":\"The profile is loaded\",\"loadedDetail\":\"The main screen's switches show it. Steam's Play button uses it too.\",\"loadedDetailDirect\":\"The main screen's switches show it.\",\"loadFailed\":\"Could not load {name}\",\"loadFailedProfile\":\"Could not load the profile\",\"loadIt\":\"Load it\",\"notLoadedLoad\":\"{name} is not the loaded profile: Load it switches them on in this game.\",\"notLoaded\":\"{name} is not the loaded profile.\",\"itNotLoadedLoad\":\"It is not the loaded profile: Load it switches them on in this game.\",\"itNotLoaded\":\"It is not the loaded profile.\",\"addedTo\":\"Added {names} to {profile}\",\"addedToProfile\":\"Added {names} to the profile\",\"alreadyIn\":\"Already in {profile}\",\"alreadyInProfile\":\"Already in the profile\",\"notInGame\":\"Kiln is not in this game\",\"installFirst\":\"Install Kiln first\",\"resumed\":\"Your mod list is loaded again.\",\"headingAddedOn\":\"Added and switched on\",\"onNextPlay\":\"On at the next Play.\",\"headingAlreadyOn\":\"Already on\",\"alreadyOn.one\":\"{names} is on already\",\"alreadyOn.other\":\"{names} are on already\",\"noProfileLoaded\":\"No profile is loaded in this game, so nothing is switched on.\",\"addToProfile\":\"Add to a profile\",\"headingNothing\":\"Nothing was added\",\"nothingToAdd\":\"Nothing to add\",\"dropsUnavailable\":\"File drops unavailable\",\"veilOff\":\"Added switched off\",\"dropToAdd\":\"Drop to add\",\"modpackInstalled\":\"Modpack installed\",\"installModpack\":\"Install this modpack?\",\"isActive\":\"{name} is active.\",\"file\":\"File\",\"server\":\"Server\",\"modpack\":\"Modpack\",\"cantRead\":\"Can't read it\",\"newMods\":\"New: {list}.\",\"allInLibrary\":\"Everything in it is in your library already.\",\"nativeCode\":\"It contains native code (DLLs).\",\"runsCode\":\"This runs code on your PC\",\"trustOnly\":\"Install only modpacks you trust.\",\"trustToggle\":\"I trust it and want its mods\",\"closeGame\":\"Close the game first.\"},\"errorBoundary\":{\"title\":\"This page failed\",\"tryAgain\":\"Try again\"},\"firstRun\":{\"noneFound\":\"No Enshrouded found\",\"addByHand\":\"Add its folder by hand.\",\"listNotPrepared\":\"Your mod list could not be prepared\",\"dropsWaiting\":\"Dropped mods are still waiting\",\"installedNotPrepared\":\"Kiln installed, but your mod list could not be prepared\",\"noRuntime\":\"No Kiln runtime in this launcher.\"},\"importResult\":{\"namesTwo\":\"{a} and {b}\",\"namesMany\":\"{count} mods\",\"fileWhy\":\"{file}: {why}\",\"fileLeftOut\":\"{file} was left out.\",\"filesLeftOut\":\"{count} files were left out.\",\"notNeededCount\":\"{count} things in it are not needed.\",\"details\":\"Details\",\"lastAddsTip\":\"What your last adds took, and what they left out and why\",\"lastAdds\":\"Last adds\",\"lastAddsHint\":\"The last {count} times you added mods, newest first. Kiln's launcher.log has the same lines.\",\"headingList\":\"{heading}: {list}\",\"nLeftOut\":\"{count} left out\",\"nNotNeeded\":\"{count} not needed\",\"whatAdded\":\"What was added\",\"nothing\":\"Nothing.\",\"leftOut\":\"Left out\",\"notNeeded\":\"Not needed\",\"noProfileMade\":\"No profile made\",\"madeWithout\":\"{profile} was made, without {label}\",\"madeWithoutDetail\":\"{label} is in your Library. Pick {profile} here to add it again, or switch it on in the list.\",\"whichProfile\":\"Add to which profile?\",\"makeAndAdd\":\"Make and add\",\"notNow\":\"Not now\",\"newName\":\"Name of the new profile\",\"newNameHint\":\"An empty profile with {label} in it, on.\",\"inListOff\":\"{label} is in your list, off. It loads once it is on in the profile you play.\",\"loaded\":\"loaded\",\"newProfile\":\"New profile…\"},\"libraryChanged\":{\"useChanged\":\"Use the changed files\",\"uses\":\"{name} uses the changed files\",\"nowVersion\":\"Now version {version}.\",\"changedAgain\":\"{name} changed again\",\"failed\":\"Could not use the changed files of {name}\",\"title\":\"{name} was changed in its folder\",\"detail\":\"Its files are not the ones Kiln imported, so Kiln does not load it as it is. Use the changed files, or put the old files back.\"},\"modFiles\":{\"filterName\":\"Mods\",\"words\":\"zip, 7z, rar, folder or DLL\"},\"mods\":{\"emberNoAbi\":\"Ember has published no mod ABI yet\",\"notInLibraryWhy\":\"it is not in the library any more\",\"notInLibrary\":\"Not in the library\",\"notInLibraryTip\":\"Its files are gone from the Library. Add it again, or remove it.\",\"loaderCannot\":\"its loader cannot start it yet\",\"notYet\":\"Does not run yet\",\"notYetTip\":\"{why}. It stays in your list.\",\"xhlWhy\":\"it runs only with XHL-Core, installed separately\",\"needsXhl\":\"Needs XHL-Core\",\"xhlTip\":\"XHL companions run only through XHL-Core. Kiln never loads their DLLs.\",\"withWorld\":\"Starts with your world\",\"offHere\":\"Off in this game\",\"itNeeds\":\"it needs {dep}\",\"needs\":\"Needs {dep}\",\"willNotRun\":\"Will not run\",\"fromGameFolder\":\"Runs from the game folder\",\"failedLast\":\"Failed at the last start\",\"leftInFolder\":\"Left in the game folder\",\"partly\":\"Only partly runs\",\"changed\":\"Changed on disk\",\"changedTip\":\"Its files were changed in its folder since Kiln imported them. Use the changed files to load them, with the name and info they give now.\",\"foundLoads\":\"Loads from the game folder\",\"foundOff\":\"Does not load\",\"foundInProfile\":\"A copy is in your list\",\"foundCannotRun\":\"Can't run\",\"namesTwo\":\"{a} and {b}\",\"namesMore\":\"{a}, {b} and {more} more\",\"countOn.one\":\"{count} mod on.\",\"countOn.other\":\"{count} mods on.\",\"countOnOff.one\":\"{count} mod on, {off} off.\",\"countOnOff.other\":\"{count} mods on, {off} off.\",\"countWorld.one\":\"{names} starts with your world.\",\"countWorld.other\":\"{names} start with your world.\",\"countStays.one\":\"{names} stays off in this game.\",\"countStays.other\":\"{names} stay off in this game.\",\"countYet.one\":\"{names} does not run yet: {why}.\",\"countYet.other\":\"{names} do not run yet: {why}.\",\"countWontOne\":\"{name} will not run: {why}.\",\"countWontMany\":\"{names} will not run in this game.\",\"bulkNoSwitch\":\"in the game folder, it has no switch\",\"bulkOnlyKept\":\"only kept versions are in the Library; pick a version explicitly\",\"bulkNoVersion\":\"no version of it is in the Library\",\"emfMissing\":\"Needs {dep}, which isn't installed.\",\"emfOff\":\"Needs {dep}, which is switched off.\",\"emfNotLoading\":\"Needs {dep}, which won't load.\",\"emfTooOld\":\"Needs {dep}; you have {found}, which is too old.\",\"emfTooNew\":\"Needs {dep}; you have {found}, which is too new.\",\"emfWithEmf\":\"It comes with EMF's release: add EMF's zip with Add mods.\",\"emfUpdateWithEmf\":\"A newer one comes with EMF's release: add EMF's zip with Add mods.\",\"emfSwitchOn\":\"Switch it on in this list.\",\"emfNotLoadingTip\":\"Its own row says why.\",\"emfTooNewTip\":\"Look for an update of this mod.\",\"emfSays\":\"EMF says: {reason}.\",\"emfWhyMissing\":\"it needs {dep}, which isn't installed\",\"emfWhyOff\":\"it needs {dep}, which is switched off\",\"emfWhyNotLoading\":\"it needs {dep}, which won't load\",\"emfWhyVersion\":\"it needs {dep} and you have {found}\",\"emfWhyWithEmf\":\"{why} (it comes with EMF's release: add EMF's zip with Add mods)\",\"emfLua\":\"EMF's lua mod\",\"emfGet\":\"Get EMF\",\"emfGetTip\":\"Opens EMF's page, where its release zip is\",\"emfModPage\":\"Mod page\",\"emfModPageTip\":\"Opens the page the mod gives for {dep}\",\"emfRejectedLast\":\"Did not load at the last start\"},\"modUpdates\":{\"found.one\":\"{count} mod has an update\",\"found.other\":\"{count} mods have an update\",\"foundDetail\":\"Each one's page is a click away in the list. Nothing was downloaded.\",\"none\":\"No updates found\",\"unchecked.one\":\"{count} mod could not be checked.\",\"unchecked.other\":\"{count} mods could not be checked.\",\"checkFailed\":\"The update check failed\",\"stale\":\"Update check is stale\",\"unknown\":\"Update unknown\",\"badgeTip\":\"{detail} Opens its page; nothing is downloaded.\",\"badgeAria\":\"{word} for {name}: open its page\",\"checkedThere\":\"{name} is checked there now\",\"title\":\"Updates of {name}\",\"openPage\":\"Open page\",\"sourceRemoved\":\"Your source is removed\",\"removeSource\":\"Remove my source\",\"saveCheck\":\"Save and check\",\"state\":\"State\",\"installed\":\"Installed\",\"published\":\"Published\",\"source\":\"Source\",\"sourceValue\":\"{provider}: {destination} ({origin}, {auth})\",\"originManual\":\"set by you\",\"originReceipt\":\"from its install\",\"originManifest\":\"from its manifest\",\"withAccount\":\"with your account\",\"anonymous\":\"anonymous\",\"noSource\":\"None\",\"lastAnswer\":\"Last answer\",\"sourceChanged\":\"Its update source changed\",\"sourceAccepted\":\"The new source is accepted\",\"useNewSource\":\"Use the new source\",\"sourceChangedBody\":\"This version names {destination}. Kiln checks it there only after you say so: a copy of a mod can point at someone else's releases.\",\"changeWhere\":\"Change where this mod is checked?\",\"keepOld\":\"Keep the old source\",\"changeIt\":\"Change it\",\"notSaved\":\"Not saved\",\"where\":\"Where it is published\",\"whereHint\":\"Kiln only reads there and shows a notice with a link. It never downloads or installs a version.\",\"githubReleases\":\"GitHub releases\",\"nexusFile\":\"A file on Nexus Mods\",\"nexusOff\":\"Nexus Mods is switched off in this build\",\"nexusOffBody\":\"This mod's saved source is the Nexus Mods file {destination}. Kiln keeps it, but does not check it in this build. Save a GitHub source here to check there instead.\",\"repository\":\"Repository\",\"repositoryHint\":\"owner/repo, as in github.com/owner/repo.\",\"releaseTag\":\"Release tag\",\"releaseTagHint\":\"With {{version}} where the version stands. Empty: v{{version}}.\",\"fileName\":\"File name\",\"fileNameHint\":\"The mod's zip in a release, {{version}} allowed. Empty: a link and a version only.\",\"versions\":\"Versions\",\"stableOnly\":\"Stable only\",\"preToo\":\"Pre-releases too\",\"modPage\":\"Mod page number\",\"modPageHint\":\"The number in nexusmods.com/enshrouded/mods/<number>.\",\"fileId\":\"File id\",\"fileIdHint\":\"The id of the file whose versions are followed. Checked with your Nexus Mods sign-in, for mods installed from the Catalog.\"},\"notFound\":{\"title\":\"Nothing here\",\"hint\":\"This page does not exist.\",\"back\":\"Back to Play\"},\"placeholder\":{\"notBuilt\":\"Not built yet\"},\"serversIndex\":{\"none\":\"No servers\",\"hint\":\"Local and remote dedicated servers show here.\"},\"replaceDll\":{\"label\":\"replaced DLL, {date}\"},\"removeMod\":{\"userLoaded\":\"{name} (loaded in {game})\",\"userPlayOnce\":\"{name} (play-once in {game})\",\"goneAlready\":\"Its files are gone from the Library already; it leaves your list.\",\"checkFailed\":\"Kiln could not check what else uses it ({error}). Remove stops if something does.\",\"checking\":\"Checking what uses it.\",\"leavesListed.one\":\"Its files leave the Library. {others} lists it too and will show it as missing.\",\"leavesListed.other\":\"Its files leave the Library. {others} list it too and will show it as missing.\",\"leaves\":\"Its files leave the Library.\",\"notSaved\":\"Your mod list could not be saved; nothing was removed.\",\"notRemovable\":\"in the game folder, Kiln did not place it: remove it there\",\"settingsToo\":\"Remove its settings too\",\"settingsTooHint\":\"Its settings in this game and your profiles go too. Kiln keeps a copy in backups.\",\"settingsTooMany\":\"Remove their settings too\",\"settingsTooManyHint\":\"Their settings in this game and your profiles go too. Kiln keeps a copy in backups.\",\"settingsLeft\":\"Some settings stayed\"},\"rollback\":{\"settingsBack.one\":\"Its settings from before the update come back with it ({count} file).\",\"settingsBack.other\":\"Its settings from before the update come back with it ({count} files).\",\"kept\":\"{version} is kept: roll back again to return to it.\",\"backOn\":\"{name} is back on {version}\",\"failed\":\"Could not roll back {name}\",\"onAgain\":\"{name} is on {version} again\",\"changedSince\":\"{name} was changed since the roll back.\"},\"ownFiles\":{\"title\":{\"one\":\"A file of the launcher is missing\",\"other\":\"Files of the launcher are missing\"},\"text\":{\"one\":\"{files} is gone from the launcher's folder. An antivirus may have quarantined it. Restore it from the quarantine and add an exclusion, or install the launcher again.\",\"other\":\"{files} are gone from the launcher's folder. An antivirus may have quarantined them. Restore them from the quarantine and add an exclusion, or install the launcher again.\"}},\"runtimeBlocked\":{\"file\":\"Windows did not let Kiln write {file}.\",\"why\":\"An antivirus or another program may be blocking it.\",\"fix\":\"Add an exclusion for the game folder in your antivirus, then try again.\",\"raw\":\"Details: {error}\"},\"runtimeChange\":{\"alreadyRunning\":\"A runtime change is already running\",\"installOf\":\"Install of {version}\",\"rollbackTo\":\"Rollback to {version}\",\"updateTo\":\"Update to {version}\"},\"selection\":{\"dropped\":\"{count} not shown any more left the selection\"},\"sfSelfHosted\":{\"text\":\"ShroudForge's own loader is installed ({file}). If it loads, Kiln's adapter steps aside and ShroudForge reads its own mods folder. Profile switches still update copies Kiln manages. Folders you installed yourself keep their own switches. Remove that loader or let Kiln manage ShroudForge to use Kiln's adapter.\"},\"shroudlinkNotice\":{\"notice\":\"Shroudlink's MODS tab comes from its client addon, which this profile does not load. Your Shroudlink mods still run; switch the addon on to get the tab.\",\"noticeDownload\":\"Shroudlink's MODS tab comes from its client addon, which this profile does not load. Your Shroudlink mods still run; switch the addon on to get the tab. Get the client addon from the Shroudlink download and add it to the Library.\"},\"snapshotGate\":{\"title\":\"The copy from before the beta could not be completed\",\"goOn\":\"Go on without a way back\",\"held\":\"Settings and profiles are held until you decide. Going on records that this beta has no complete copy to restore.\"},\"startPanel\":{\"barAria\":\"Starting the game\",\"stepsAria\":\"Steps of the start\",\"cookingMods\":\"Cooking mods\"},\"stoppedStart\":{\"rosterNames\":\"These mods add more NPCs to the Survivors roster than the game can hold: {names}\",\"roster\":\"These mods add more NPCs to the Survivors roster than the game can hold.\",\"contentNames\":\"Kiln could not build the NPCs and quests of these mods, so no world was loaded: {names}\",\"content\":\"Kiln could not build the NPCs and quests of the mods, so no world was loaded.\",\"reasonNames\":\"{reason} ({names})\",\"review\":\"Review these mods\"},\"closeGuard\":{\"title\":\"Close Kiln?\",\"busy\":\"The save editor is writing a save right now. Wait until it is done before you close Kiln.\",\"studio\":\"Pack Studio has unsaved changes. Closing Kiln now throws them away.\",\"editor\":\"An editor has unsaved changes. Closing Kiln now throws them away.\",\"saveEditor\":\"Your changes to the character in the save editor are not saved. Closing Kiln now throws them away.\",\"saveEditorNamed\":\"Your changes to {name} in the save editor are not saved. Closing Kiln now throws them away.\",\"close\":\"Close anyway\",\"stay\":\"Keep Kiln open\"},\"updateNotice\":{\"updated\":\"Updated\",\"restart\":\"Restart\",\"trayAria\":\"Launcher update: {word}\",\"updating\":\"Updating…\",\"upToDate\":\"Up to date\",\"updateFailed\":\"Update failed\",\"skipFailed\":\"Skip failed\",\"offerTitle\":\"Kiln {build}\",\"skip\":\"Skip\",\"download\":\"Download\",\"readyTitle\":\"Ready to restart\",\"waitingFor\":\"Waiting for {task}\",\"updatedTo\":\"Updated to {build}\",\"switchBack\":\"This game has Kiln {from} from the test build; this installs Kiln {to}.\",\"closeGame\":\"Close the game first\",\"updateGame\":\"Update game\",\"restartingIn\":\"Restarting by itself in {seconds} s\",\"holdGame\":\"The game is running, so the launcher does not restart by itself. Restart when you like: the new Kiln goes into the game once it closes.\",\"holdLaunch\":\"The game is starting, so the launcher waits.\",\"holdWriting\":\"The save editor is writing a save, so the launcher waits.\",\"holdStudio\":\"Pack Studio has unsaved changes, so the launcher waits. Save them, then restart.\",\"holdEditor\":\"An editor has unsaved changes, so the launcher waits. Save them, then restart.\",\"lookingAtGame\":\"Looking at the game's Kiln…\",\"applying\":\"Putting Kiln {version} into the game…\",\"afterGame\":\"The game is running: Kiln {version} goes into the game when it closes.\",\"afterSteam\":\"Steam is updating the game: Kiln {version} goes in when it is done.\",\"appliedLead\":\"It came with the launcher update to {build}.\"},\"useMods\":{\"notLoadedNow\":\"{name} is not the loaded profile now\",\"stillSaving\":\"the profile is still being saved\",\"changedElsewhere\":\"{name} changed elsewhere since\",\"undoneAlready\":\"it was undone already\"},\"parts\":{\"unavailable\":\"component {name} is unavailable for the {side} side\"}}"),
	format: {
		ago: {
			justNow: "just now",
			minutes: "{n} min ago",
			hours: "{n} h ago",
			yesterday: "yesterday"
		},
		installNote: {
			one: "{count} changed file kept in backups",
			other: "{count} changed files kept in backups"
		}
	},
	ui: {
		brand: { starting: "Starting" },
		button: { loading: "Loading" },
		dropZone: {
			dropHere: "Drop files here",
			dropsUnavailable: "File drops unavailable",
			cannotOpen: "Could not open files",
			needsDesktop: "Needs the desktop app",
			files: "Files",
			folder: "Folder",
			browse: "Browse"
		},
		form: {
			clearNamed: "Clear {label}",
			clear: "Clear",
			clearTitle: "{word} (Esc)",
			flameLight: "Light {label}",
			flameOnTitle: "{label} is on: switch it off",
			flameOffTitle: "{label} is off: switch it on",
			flameLitTitle: "{label} is lit: put it out",
			flameOutTitle: "{label} is out: light it"
		},
		jsonView: {
			items: "{n} items",
			keys: "{n} keys"
		},
		logView: {
			log: "Log",
			noMatch: "Nothing matches that filter.",
			empty: "Nothing written yet.",
			follow: "Follow"
		},
		modal: { typeToConfirm: "Type {word} to confirm" },
		sortableList: {
			reorder: "Reorder {name}",
			dragHint: "Drag, or Alt+Up/Down",
			moveUp: "Move up",
			moveDown: "Move down"
		},
		table: {
			ascending: "Ascending",
			descending: "Descending",
			empty: "Nothing here."
		},
		schemaForm: {
			notOnOff: "Not on/off",
			notNumber: "Not a number",
			wholeOnly: "Whole numbers only",
			min: "Min {min}",
			max: "Max {max}",
			notOption: "Not an option",
			durationLike: "Like \"30s\", \"5m\", \"1h30m\"",
			notList: "Not a list",
			notText: "Not text",
			noMatch: "No setting matches that.",
			noSettings: "This config has no settings.",
			synced: "Synced",
			syncedTitle: "Sent to Kiln clients",
			restart: "Restart",
			restartTitle: "Applies after a restart",
			pending: "Pending",
			pendingTitle: "Saved; waits for a restart",
			default: "Default {value}",
			resetLabel: "Reset {name} to its default",
			removeItem: "Remove {item}",
			addItem: "Add {type}",
			more: "More",
			less: "Less"
		},
		taskProgress: {
			titleFailed: "{title} failed",
			titleCanceled: "{title} canceled",
			taskFailed: "task failed",
			done: "Done",
			failed: "Failed",
			canceled: "Canceled",
			titleState: "{title}: {state}",
			cancelTask: "Cancel {title}",
			andMore: "{title} and {count} more",
			tasks: "Tasks",
			tasksLabel: "Tasks: {label}",
			clearFinished: "Clear finished"
		},
		toast: {
			times: " ({count} times)",
			earlier: {
				one: "{count} earlier notice",
				other: "{count} earlier notices"
			},
			keptFiles: "Files your mods wrote were kept",
			keptFolders: "Some mod folders were kept",
			keptFoldersDetail: "{detail}. These mods may still run through Kiln or another loader. With the game closed, review or move the listed folders to stop them; your settings have not been deleted.",
			showLess: "Show less",
			showAll: "Show all"
		},
		banner: {
			hide: "Hide",
			show: "Show"
		}
	},
	api: {
		channel: {
			name: {
				beta: "Beta (test builds)",
				release: "Release",
				stableNone: "Stable (none yet)",
				devBuilds: "Dev builds"
			},
			word: {
				beta: "Beta",
				release: "Release",
				stable: "Stable",
				dev: "Dev"
			},
			buildBeta: "{version} beta {n}",
			worldWarning: "A world you play with a test build may not load the same way on the release, and servers and players must run the same build to play together.",
			toBeta: {
				title: "Switch to the beta?",
				what: "Test builds of the next release, for testing with the Kiln Discord. The launcher installs the newest one and restarts. To test its Kiln in your game, install it on the Runtime page.",
				back: "You can switch back to the release here at any time.",
				confirm: "Switch now",
				task: "Switch to the beta"
			},
			back: {
				title: "Switch back to the release?",
				what: "The launcher installs the newest release and restarts, also when it is older than this test build. {advice}",
				confirm: "Switch back",
				task: "Switch back to the release"
			},
			restore: {
				label: "Put back my profiles and settings from before the beta",
				hint: "Saved {when}, before the first start of a test build. Mods you added in the beta stay in the Library."
			},
			notice: {
				switchBack: "This game has Kiln {from} from a test build; Play puts this launcher's Kiln {to} in first.",
				betaLauncher: "This launcher is a test build; this game still has the release's Kiln. Install Kiln {version} on Runtime to test it."
			},
			advice: {
				unknown: "Releases before 0.2.0-dev.29 leave the test build's Kiln in your game: reinstall the release's Kiln on the Runtime page. Newer releases put their own Kiln in at the next Play.",
				automatic: "Kiln in your game goes back with it at the next Play.",
				manual: "This release leaves the test build's Kiln in your game. Reinstall the release's Kiln on the Runtime page."
			}
		},
		kinds: {
			notRunning: { ember: "Ember mods don't run yet: Ember has not published its mod ABI, so kiln-ember only finds and reports them. They stay in your list; running them will require a compatible loader update." },
			name: {
				native: "Native",
				plugin: "Plugin",
				eml: "EML (Enshrouded Mod Loader)",
				xhl: "XHL (requires XHL-Core)",
				ember: "Ember (server SDK)"
			},
			short: {
				native: "Native",
				plugin: "Plugin"
			}
		},
		nexus: { type: {
			kiln: "Kiln mod",
			native: "Native mod",
			emf: "EMF mod",
			dll: "DLL mod",
			modpack: "Modpack",
			notKiln: "Not a Kiln mod"
		} },
		updates: {
			reason: {
				incompleteList: "the release list is too long to read to its end",
				invalidMetadata: "the newest release's kiln-release.json is not valid for this mod",
				missingAsset: "the newest release has no file with the expected name",
				ambiguousAsset: "the newest release has several files with the expected name",
				ambiguousRelease: "several releases share the highest version",
				nonSemver: "the versions are not semantic versions, so they cannot be ordered",
				sameVersionUnbound: "the same version is published, and Kiln does not know which upload of it you have",
				network: "no answer (offline, or not signed in where a sign-in is needed)",
				sourceConflict: "its update source changed and waits for your yes",
				rateLimited: "the provider's request limit is used up for now",
				notFound: "no repository or no matching release was found",
				notChecked: "not checked yet",
				releaseRemoved: "the release this copy came from is gone",
				unknown: "unknown reason"
			},
			stale: "Not confirmed lately.",
			staleReason: "Not confirmed lately: {reason}.",
			word: {
				update: "Update",
				updateStale: "Update (stale)",
				updateTo: "Update {version}",
				updateToStale: "Update {version} (stale)",
				newUpload: "New upload",
				newUploadStale: "New upload (stale)",
				current: "Up to date",
				unsupported: "No update source",
				disabled: "Checks off",
				unknown: "Unknown",
				unknownStale: "Unknown (stale)"
			},
			detail: {
				available: "Version {candidate} is published; you have {installed}.",
				incompatible: "Its release says it does not load on this Kiln.",
				newUpload: "Version {installed} was published again as another file.",
				current: "{installed} is the newest published version.",
				unsupported: "Kiln does not know where this mod is published. Set a source to get update notices.",
				disabled: "Its provider is switched off in this build.",
				unknown: "Kiln cannot tell whether an update exists: {reason}."
			}
		},
		oneActivation: { busy: "Kiln is still loading a profile in this game. Try again when that is done." },
		commands: { selfUpdateDisabled: "Self-update is disabled in this build" },
		admin: {
			sessionClosed: "The admin session closed ({reason}).",
			sessionClosedReconnecting: "The admin session closed ({reason}); reconnecting…",
			notConnected: "Not connected to the server's Kiln.Admin.",
			needsNewer: "This needs a newer Kiln.Admin on the server (it has no '{op}' op yet).",
			noTransport: "This app has no connection to Kiln.Admin set up."
		},
		adminWs: {
			unreachable: "The admin panel's connection to the server could not open ({reason}).",
			handshake: "The server did not finish opening the admin connection within {seconds} s.",
			closedEarly: "The server closed the admin connection before it was ready ({reason}).",
			reconnecting: "Reconnecting to the server…",
			noSession: "No admin session {session}.",
			connectionClosed: "The admin connection closed.",
			timeout: "No answer within {seconds} s.",
			tooLarge: "The request is too large for the server ({bytes} bytes, at most {max}).",
			noTopic: "The server offers no '{topic}' topic.",
			silent: "the server was silent for {seconds} s",
			closedBy: "the connection closed ({code})",
			opFailed: "The op failed."
		}
	},
	logs: {
		tab: {
			game: "Game",
			proxy: "Proxy",
			mods: "Kiln mods",
			crash: "Crashes",
			ticks: "Tick stats",
			engines: "Engines"
		},
		title: "Logs",
		noGame: "No game yet. Find Enshrouded on Play first.",
		say: {
			ticks: "Kiln writes a line each minute a world runs.",
			engines: "Other engines' plugins: what each one did at the last start.",
			crash: "Kiln writes one when a mod faults too often.",
			file: "{name}, {size}, written {ago}.",
			notYet: "Not written yet: it appears after the next start."
		},
		openFolder: "Open folder",
		tabsLabel: "Log",
		unreadable: "The logs are not readable",
		noFile: "No file",
		fileLabel: "Log file",
		follow: "Follow",
		pause: "Pause",
		filter: "Filter",
		notYetTitle: "Not written yet. It appears after the next start.",
		olderCut: "… (older lines cut)",
		cantRead: "Can't read it",
		crashes: {
			report: "Report",
			when: "When",
			size: "Size",
			unreadable: "The reports are not readable",
			label: "Crash reports",
			empty: "No crash reports. Kiln writes one when a mod faults too often."
		}
	},
	about: {
		lucideIcons: "Lucide icons",
		mitOrApache: "MIT or Apache-2.0",
		version: "Version",
		launcher: "Launcher",
		commit: "Commit",
		platform: "Platform",
		newVersions: "New versions",
		updates: "Updates",
		backend: "Backend",
		browserMock: "Browser mock",
		data: "Data",
		launcherUses: "The launcher uses",
		tagline: "Mods for Enshrouded. Theme and design: frostdev.",
		credits: "Credits",
		creditsFrom: "From CREDITS.md"
	},
	saves: {
		kind: {
			world: "world",
			character: "character",
			otherKind: "other"
		},
		title: "Saves",
		noGame: "No game yet. Find Enshrouded on Play first.",
		col: {
			file: "File",
			kind: "Kind",
			changed: "Changed",
			size: "Size"
		},
		sub: {
			one: "{count} save file. Kiln backs them up before a start with different mods and keeps the last ones.",
			other: "{count} save files. Kiln backs them up before a start with different mods and keeps the last ones."
		},
		files: "Save files",
		steamUser: "Steam user {id}",
		openFolder: "Open folder",
		unavailable: "Saves unavailable",
		none: "No saves found"
	},
	ticks: {
		now: "now",
		minAgo: "{n} min ago",
		noneYetShort: "No tick stats yet. Kiln writes them each minute a world runs.",
		stepMinutes: {
			one: "Step p99, {count} minute",
			other: "Step p99, {count} minutes"
		},
		msSuffix: " ms",
		chartLabel: "Step p99 over the last {count} minutes, from {min} to {max} milliseconds, now {now}.",
		unreadable: "The tick stats are not readable",
		noneYet: "No tick stats yet.",
		noneYetHint: "Kiln writes one line each minute while a world runs.",
		col: {
			minute: "Minute",
			rate: "Steps/s",
			p50: "Step p50",
			p99: "Step p99",
			max: "Max",
			kilnP99: "Kiln p99"
		},
		ms: "{v} ms",
		lastMinute: "Last minute",
		steps: "Steps",
		maxStep: "Max step",
		subscribers: "Subscribers",
		subs: "{pre} pre, {post} post",
		world: "World",
		perMinute: "Tick stats per minute"
	},
	gameStart: {
		title: "How Kiln starts this game",
		titleAdded: "A custom game folder",
		docs: "About custom folders",
		later: "Keep the default",
		customIntro: "This copy of the game is one Kiln starts itself.",
		how: "Start",
		steam: "Through Steam",
		direct: "Start the game directly",
		steamHint: "Play asks Steam to start Enshrouded, as Steam's own Play button does.",
		steamCustomHint: "Steam starts the copy in its own library, not this folder: Play would start another copy of the game.",
		directHint: "Play starts this folder's enshrouded.exe itself, after the profile and save backup steps. Kiln's proxy is in the folder, so Kiln loads with the game.",
		directOnly: "Kiln starts the exe and sets up nothing else: whatever this copy of the game needs to run is up to the copy.",
		runner: "Proton or Wine",
		runnerHint: "What runs the Windows game on Linux: umu-run, a Proton build or Wine.",
		runnerAuto: "Automatic ({name})",
		runnerNone: "Automatic (none found)",
		autoUmu: "Automatic picks umu-run when it is installed, else the newest Proton found, else Wine. Now: umu-run, running {proton}.",
		autoProton: "Automatic picks umu-run when it is installed, else the newest Proton found, else Wine. Now: {name}, the newest Proton found.",
		autoWine: "Automatic picks umu-run when it is installed, else the newest Proton found, else Wine. Now: Wine, as neither umu-run nor a Proton was found.",
		umuWith: "umu-run with {proton}",
		umuOwnProton: "umu's own Proton",
		fromCompat: "compatibility tools",
		fromUmu: "umu-launcher",
		fromValve: "Valve",
		fromPath: "your path",
		runnerOther: "Another umu-run, Proton or Wine…",
		runnerPath: "umu-run, Proton folder or wine program",
		runnerPathHint: "A umu-run program, a Proton folder (the one with the proton script), or a wine program.",
		prefix: "Prefix",
		prefixOwn: "Kiln's own",
		prefixPick: "Pick a folder",
		prefixOwnHint: "A prefix of its own for this game, made at the first start: {path}. Saves of this copy live in it.",
		prefixPickHint: "For Proton the folder that holds pfx (a compatdata folder), for umu-run and Wine the prefix itself.",
		cannotStart: "Kiln can't start it directly yet",
		command: "What Play runs",
		commandWindows: "What Play runs (in the game folder)",
		savedDirect: "Kiln starts this game directly now",
		savedSteam: "Kiln starts this game through Steam now",
		unknownBuild: "Kiln doesn't know this game build ({hash}). Kiln starts with the game but stays idle: no mods load.",
		unknownBuildTitle: "Unknown game build",
		readMore: "Read more",
		word: {
			steam: "through Steam",
			direct: "directly"
		}
	},
	community: /* @__PURE__ */ JSON.parse("{\"nav\":\"Community\",\"navCount\":{\"one\":\"{count} new reply from Kiln support\",\"other\":\"{count} new replies from Kiln support\"},\"title\":\"Community\",\"say\":{\"out\":\"Kiln's Discord: help from the Kiln team and from other players. All of it is optional.\",\"in\":\"Signed in as {name}.\",\"unread\":{\"one\":\"Signed in as {name}. {count} new reply from Kiln support.\",\"other\":\"Signed in as {name}. {count} new replies from Kiln support.\"},\"offline\":\"You're offline. Your posts show as they were at {time}.\",\"offlineOut\":\"You're offline. This page needs the internet.\",\"relay\":\"Kiln support isn't answering right now. Your posts show as they were at {time}.\"},\"join\":\"Join the Kiln Discord\",\"openDiscord\":\"Open the Kiln Discord\",\"help\":{\"title\":\"Get help\",\"sub\":\"Kiln support answers in #support on the Kiln Discord.\",\"text\":\"Send a report and the launcher writes the post for you: your words, what the report found, and your logs for the Kiln team.\",\"send\":\"Send a report\",\"save\":\"Save report instead\",\"needsSignIn\":\"Sending needs a Discord sign-in. You can write the report first.\"},\"posts\":{\"title\":\"Your support posts\",\"emptyOut\":\"Sign in to see your posts and their replies here.\",\"empty\":\"No posts yet. A report you send shows here with its replies.\",\"status\":{\"waiting\":\"Waiting\",\"replied\":\"New reply\",\"repliedSeen\":\"Replied\",\"solved\":\"Solved\",\"closed\":\"Closed\"},\"lastReply\":\"{name} replied {ago}.\",\"lastReplyTeam\":\"{name} of the Kiln team replied {ago}.\",\"noReply\":\"Sent {ago}. No reply yet.\",\"replies\":{\"one\":\"{count} reply\",\"other\":\"{count} replies\"},\"open\":\"Open in Discord\",\"more\":\"More for {title}\",\"copyLink\":\"Copy link\",\"fixed\":\"It's fixed\",\"fixedDone\":\"Marked as solved\",\"fixedDetail\":\"Your post has the Solved tag now. Thanks for saying so.\",\"delete\":\"Delete my post\",\"deleteAsk\":\"Delete this post?\",\"deleteText\":\"The post and its replies are removed from Discord. The Kiln team's copy of your report zip is deleted too.\",\"deleted\":\"Post deleted\",\"copied\":\"Link copied\",\"asOf\":\"As of {time}\"},\"news\":{\"title\":\"News\",\"all\":\"All releases\",\"offline\":\"News loads when you're back online.\"},\"events\":{\"title\":\"Events\",\"empty\":\"No events planned right now.\"},\"links\":{\"title\":\"Links\",\"docs\":\"Kiln's docs\",\"issues\":\"Bug reports on GitHub\",\"nexus\":\"Kiln on Nexus Mods\",\"download\":\"Download page\"},\"account\":{\"title\":\"Discord\",\"out\":\"Not signed in.\",\"outHint\":\"Sign in so the launcher can post your reports and show their replies here. It can't read your messages or see your servers.\",\"signIn\":\"Sign in with Discord\",\"alsoJoin\":\"Also join the Kiln Discord\",\"member\":\"In the Kiln Discord\",\"notMember\":\"Not in the Kiln Discord yet\",\"tester\":\"Tester\",\"signOut\":\"Sign out\",\"forget\":\"Sign out and forget me\",\"signedOut\":\"Signed out\",\"signedOutDetail\":\"The launcher holds nothing of your Discord account now.\",\"whatIsSent\":\"What is sent and kept\"},\"notify\":{\"label\":\"Tell me when Kiln support replies\",\"hint\":\"A system notification, also while the launcher is in the background.\",\"title\":\"Kiln support replied\",\"detail\":\"{by} replied to “{title}”.\"},\"presence\":{\"title\":\"Rich Presence\",\"label\":\"Show Kiln on my Discord profile\",\"hint\":\"While you play: Enshrouded with Kiln, how many mods are on, and for how long.\",\"on\":\"On. It shows while you play.\",\"off\":\"Off.\",\"noDiscord\":\"Discord isn't running on this PC. It shows once Discord is open.\",\"noDiscordDeck\":\"Discord isn't running on this Deck. It shows once Discord is open.\",\"preview\":\"How others see it on your Discord profile\",\"never\":\"Never shown: server names, world names, your character, or which mods you use.\",\"playing\":\"Playing\",\"name\":\"Kiln\",\"details\":\"Enshrouded with Kiln\",\"state\":{\"one\":\"{count} mod on\",\"other\":\"{count} mods on\"},\"elapsed\":\"{time} elapsed\",\"getKiln\":\"Get Kiln\",\"joinDiscord\":\"Join the Discord\",\"onToast\":\"Rich Presence is on\",\"offToast\":\"Rich Presence is off\",\"pack\":{\"label\":\"Show my Crucible pack on my Discord profile\",\"hint\":\"While you play a pack from Crucible: its name, and a Get this modpack button that opens the pack's page. Packs made on this PC are never named.\",\"never\":\"Never shown: server names, world names or your character. Your Crucible pack's name and page are shown while you play it.\",\"state\":{\"one\":\"{pack} ({count} mod)\",\"other\":\"{pack} ({count} mods)\"},\"getPack\":\"Get this modpack\",\"example\":\"Ember run\",\"turnOn\":\"Show my pack\",\"turnOff\":\"Hide my pack\",\"onToast\":\"Your Crucible pack shows on Discord\",\"offToast\":\"Your Crucible pack no longer shows on Discord\"}},\"signIn\":{\"title\":\"Sign in with Discord\",\"text\":\"Your browser opens Discord's sign-in page through frostdev.io. You approve Kiln there, then come back here.\",\"gets\":\"Kiln gets your Discord name, ID and picture. Nothing else: not your email, your messages, your friends or your servers.\",\"joinHint\":\"Discord then also asks to let Kiln add you to the server.\",\"open\":\"Open the sign-in page\",\"waitingTitle\":\"Finish in your browser\",\"waiting\":\"Approve Kiln on the page that opened. This window moves on by itself.\",\"waitingWord\":\"Waiting for Discord\",\"again\":\"Open the page again\",\"done\":\"Signed in as {name}\",\"doneDetail\":\"Your reports can go straight to Kiln support now.\",\"canceled\":\"You cancelled on Discord's page. Nothing changed.\",\"expired\":\"The sign-in timed out after 10 minutes. Try again.\",\"denied\":\"The sign-in was declined. Nothing changed.\",\"offline\":\"You're offline. Signing in needs the internet.\",\"relay\":\"frostdev.io isn't answering. Try again in a few minutes.\",\"tooMany\":\"Too many sign-ins came from your network in the last hour. Try again in {wait}.\",\"retry\":\"Try again\",\"tabDone\":\"Signed in. You can close this tab and go back to the launcher.\",\"tabFailed\":\"The sign-in did not finish. You can close this tab and go back to the launcher.\"},\"kind\":{\"noload\":\"Kiln doesn't load\",\"mod\":\"A mod doesn't work\",\"crash\":\"The game crashes\",\"launcher\":\"The launcher\",\"server\":\"My server\",\"something\":\"Something else\"},\"send\":{\"title\":\"Send to Kiln support\",\"checking\":\"Reading your setup\",\"line\":\"What went wrong, in one line\",\"linePh\":{\"noload\":\"Kiln doesn't load since the last game update\",\"mod\":\"Big Stacks doesn't change the stack sizes\",\"crash\":\"The game crashes when I open the map\",\"launcher\":\"The launcher shows a black window\",\"server\":\"My server turns away players with Kiln\",\"something\":\"Say it in a few words\"},\"lineCount\":\"{count} of {max}\",\"lineNeeded\":\"Write one line first.\",\"kind\":\"What kind of problem?\",\"more\":\"Anything else? (optional)\",\"morePh\":\"What were you doing? Did it work before? What have you tried?\",\"next\":\"Next: check what is sent\",\"known\":{\"word\":\"Known problem\",\"fixes\":\"If this fixes it, there's nothing to send. If it doesn't, carry on.\",\"copy\":\"Copy the line\",\"copied\":\"Line copied\",\"action\":{\"mods\":\"Show my mods\",\"runtime\":\"Open Runtime\",\"logs\":\"Open the Logs page\",\"settings\":\"Open Settings\"}},\"check\":{\"title\":\"Check what is sent\",\"public\":\"In #support, for everyone\",\"publicHint\":\"Posted by the Kiln bot with you mentioned, so Discord tells you about replies.\",\"postTitle\":\"Title\",\"words\":\"Your words\",\"noWords\":\"None\",\"kind\":\"Kind\",\"tags\":\"Tags\",\"found\":\"What the report found\",\"versions\":\"Versions\",\"level\":{\"problem\":\"Problem\",\"warning\":\"Warning\",\"info\":\"Note\"},\"zip\":\"The report zip\",\"zipHint\":\"{size}, {count} files: Kiln's logs and state files, the profile, the launcher's log and lists of the game folder.\",\"showFiles\":\"Show the files\",\"hideFiles\":\"Hide the files\",\"who\":\"Who sees the report zip?\",\"team\":\"The Kiln team only\",\"teamHint\":\"Sent to a channel only the Kiln team can read. It's deleted there after 30 days.\",\"everyone\":\"Everyone in #support\",\"everyoneHint\":\"Attached to your post, so other players can look too.\",\"noZip\":\"Nobody\",\"noZipHint\":\"Only the post goes. The team may ask you for the zip.\",\"redacted\":\"In every file your user name, PC name, Steam names, Steam ID and IP addresses are replaced. Saves and mod settings are never in it. A character or server name you chose can still be in a log line.\",\"as\":\"Posting as {name}\",\"send\":\"Send to Kiln support\",\"signInToSend\":\"Sign in with Discord to send\",\"signInHint\":\"Sending needs a Discord sign-in. Your draft stays here while you sign in.\",\"sending\":\"Sending\"},\"err\":{\"offlineTitle\":\"You're offline\",\"offline\":\"Your draft is kept. Send it when you're back online, or save the report and post it yourself.\",\"relayTitle\":\"Kiln support can't be reached\",\"relay\":\"frostdev.io isn't answering. Your draft is kept. Try again in a few minutes.\",\"notMemberTitle\":\"You're not in the Kiln Discord yet\",\"notMember\":\"Replies happen on Discord, so join the server first. Then come back and press Send.\",\"joined\":\"I've joined\",\"rateTitle\":\"That's a lot of reports\",\"rate\":\"You sent {sent} reports in the last hour. Wait for a reply, or add to your last post on Discord. You can send again in {wait}.\",\"rateDay\":\"You sent {sent} reports in the last 24 hours. Wait for a reply, or add to your last post on Discord. You can send again in {wait}.\",\"rateNetwork\":\"Many reports came from your network in the last 24 hours, so sending is paused for now. You can send again in {wait}.\",\"openLast\":\"Open my last post\",\"tooNewTitle\":\"This Discord account is too new\",\"tooNew\":\"Kiln support takes reports from Discord accounts that are at least 3 days old. Your draft is kept: send it then, or save the report now.\",\"staleTitle\":\"The report was made again\",\"stale\":\"It was prepared over 30 minutes ago, so the launcher made a new one. Check what is sent, then press Send again.\",\"bigTitle\":\"The report is too big to send\",\"big\":\"This report is {size}, and up to {limit} can be sent. Send the post without the zip, then save the report and give it to the team on Discord.\",\"sendWithout\":\"Send without the zip\",\"saveReport\":\"Save report\",\"stillNot\":\"You're still not in the Kiln Discord. Join with the invite, then press I've joined.\",\"prepareTitle\":\"The report could not be made\",\"discordTitle\":\"Discord didn't accept the report\",\"discord\":\"Discord said: {reason}. This is on Kiln's side, not your connection. Your report is kept: try again later, or save the report and post it yourself.\",\"discordNoReason\":\"This is on Kiln's side, not your connection. Your report is kept: try again later, or save the report and post it yourself.\",\"discordLimitTitle\":\"Discord asks Kiln support to wait\",\"discordLimit\":\"Discord limits how often Kiln support can post, and the limit is reached right now. This is not your connection. Your report is kept: try again in {wait}.\"},\"sent\":{\"title\":\"Sent\",\"text\":\"Your post is up in #support: “{title}”.\",\"team\":\"The report zip went to the Kiln team only.\",\"public\":\"The report zip is attached to your post.\",\"none\":\"The post went without the zip.\",\"replies\":\"Replies show on the Community page, and Discord tells you too.\",\"open\":\"Open in Discord\"}},\"privacy\":{\"title\":\"What is sent and kept\",\"never\":\"Nothing is sent until you press a button on a screen that shows what goes.\",\"signInTitle\":\"When you sign in\",\"signIn\":\"Kiln gets your Discord user ID, name and picture. frostdev.io keeps them while you're signed in. It never gets your email, your password, your messages, your friends or your servers.\",\"join\":\"If you tick Also join, frostdev.io adds you to the Kiln Discord once, right then.\",\"reportTitle\":\"When you send a report\",\"reportPublic\":\"In #support, for everyone: your line, your words, the kind of problem, what the report found, the versions, how many mods are on and your Discord name.\",\"reportTeam\":\"For the Kiln team only, unless you choose otherwise: the report zip. It's deleted from the team's channel after 30 days.\",\"presenceTitle\":\"Rich Presence\",\"presence\":\"Only while it's on and you play, and only to the Discord app on this PC: Enshrouded with Kiln, how many mods are on, and the time. Never server or world names, your character, or which mods you use.\",\"presencePack\":\"With Show my Crucible pack on, also the name of the Crucible pack you play and a link to its page on frostdev.io. Packs made on this PC are never named.\",\"testerTitle\":\"When you volunteer as a tester\",\"tester\":\"The Kiln team gets this PC's graphics card, driver, video memory, system and how the game runs. Your results go to the build's post in #testing. A support report you attach goes to the team only.\",\"keptTitle\":\"What frostdev.io keeps\",\"kept\":\"A fingerprint of your sign-in, until you sign out or for 90 days unused. Your Discord ID with the list of your posts, so the launcher can show their replies. Your name and picture while you're signed in. Your hardware if you test. A Deck's sign-in code and the network it came from, for 10 minutes. Counts for the sending limits, for a day. A post leaves the list 90 days after it's solved or closed, and Sign out and forget me deletes all of it at once. Report zips are never stored on frostdev.io.\",\"undoTitle\":\"Undo any of it\",\"undo\":\"Sign out. Sign out and forget me. Delete a post from its menu. Stop testing. Turn Rich Presence off.\",\"forgetAsk\":\"Sign out and forget you?\",\"forgetText\":\"frostdev.io deletes your name, your ID, your hardware, the list of your posts and the Kiln team's copies of your report zips. Your posts stay on Discord until you delete them.\",\"alsoDelete\":\"Also delete my support posts from Discord\",\"forgetFailedTitle\":\"Signed out on this PC only\",\"forgetFailed\":\"frostdev.io didn't hear it, so it still keeps what it had. Sign in again and use Sign out and forget me, or write to admin@frostdev.io.\",\"ai\":\"The Kiln team uses AI assistants. They help read support posts and reports, and they write many of the replies. The privacy page names them.\",\"page\":\"The full privacy page\",\"terms\":\"Terms\",\"pageUrl\":\"frostdev.io/kiln/privacy\"},\"banner\":{\"send\":\"Send to Kiln support\",\"crashTitle\":\"The game closed unexpectedly\",\"crash\":\"It closed at {time}, {after} after it started. Kiln wrote a crash report.\",\"crashReport\":\"See the crash report\",\"afterSeconds\":{\"one\":\"{count} second\",\"other\":\"{count} seconds\"},\"afterMinutes\":{\"one\":\"{count} minute\",\"other\":\"{count} minutes\"},\"afterHours\":{\"one\":\"{count} hour\",\"other\":\"{count} hours\"}},\"tester\":{\"title\":\"Help test Kiln\",\"pitch\":\"Kiln's next graphics work needs hardware the team doesn't have: AMD RX 6000 and newer for FSR 4, Intel Arc for XeSS, NVIDIA RTX 40 and 50 for frame generation, and the Steam Deck.\",\"yours\":\"This PC can test {what}.\",\"yoursDeck\":\"This Steam Deck can test the Steam Deck.\",\"any\":\"Every PC helps: each test build needs a few players on it.\",\"volunteer\":\"Volunteer as a tester\",\"you\":\"You're a tester.\",\"joinText\":\"Testers get the Tester role on the Kiln Discord, the test builds, and a short checklist for each one. It takes about 15 minutes a build.\",\"hwTitle\":\"What the launcher shares about this PC\",\"hwTitleDeck\":\"What the launcher shares about this Deck\",\"hwHint\":\"Sent only when you press Volunteer, to the Kiln team, so they know what you can test.\",\"gpu\":\"Graphics card\",\"driver\":\"Driver\",\"vram\":\"Video memory\",\"os\":\"System\",\"runner\":\"Runs the game with\",\"covers\":\"Can test\",\"beta\":\"Switch the launcher to test builds\",\"betaHint\":\"You can switch back to the release in Settings at any time.\",\"send\":\"Volunteer\",\"done\":\"You're a tester\",\"doneDetail\":\"You have the Tester role. Each test build's checklist shows on the Community page.\",\"leave\":\"Stop testing\",\"leaveDone\":\"You're not a tester any more\",\"signInFirst\":\"Volunteering needs a Discord sign-in: the Tester role is on the Kiln Discord.\",\"plan\":{\"title\":\"Test {build}\",\"say\":\"{done} of {count} steps tried. Open until {until}.\",\"open\":\"Open the checklist\",\"for\":\"For {what}\",\"result\":{\"worked\":\"Worked\",\"failed\":\"Didn't work\",\"skipped\":\"Couldn't try\",\"none\":\"Not tried\"},\"note\":\"One line (optional)\",\"notePh\":\"What happened?\",\"attach\":\"Attach a support report for the Kiln team\",\"attachHint\":\"The zip goes to the team only, as with a report to support.\",\"sendResults\":\"Send results\",\"sent\":\"Results sent {ago}. The Kiln team hasn't read them yet.\",\"read\":\"{name} of the Kiln team read your results {ago}.\",\"replied\":\"They also replied in the build's post on Discord.\",\"openPost\":\"Open the build's post\",\"noPlan\":\"No test build right now. The next one shows here.\",\"hidden\":{\"one\":\"{count} step is for other hardware.\",\"other\":\"{count} steps are for other hardware.\"},\"sentToast\":\"Results sent\",\"sentToastDetail\":\"They're in the build's post in #testing. The team marks them when they've read them.\",\"how\":\"How\",\"untried\":\"Try each step, or mark it Couldn't try.\",\"more\":\"More for this test build\",\"discordTitle\":\"Discord didn't accept the results\",\"discord\":\"Discord said: {reason}. This is on Kiln's side, not your connection. Your results are kept: try again later.\",\"discordNoReason\":\"This is on Kiln's side, not your connection. Your results are kept: try again later.\",\"discordLimit\":\"Discord limits how often Kiln support can post, and the limit is reached right now. Your results are kept: try again in {wait}.\"},\"unknown\":\"Not known\"},\"deck\":{\"tab\":\"Community\",\"on\":\"On\",\"off\":\"Off\",\"turnOn\":\"Turn on\",\"turnOff\":\"Turn off\",\"sayOut\":\"Help from the Kiln team on Discord. Optional.\",\"signInSub\":\"With your phone: scan a code\",\"accountSub\":\"Signed in. In the Kiln Discord.\",\"accountSubOut\":\"Signed in. Not in the Kiln Discord yet.\",\"sendSub\":\"Your logs go to the Kiln team\",\"joinSub\":\"A code for your phone\",\"presenceSub\":\"Kiln on your Discord profile while you play\",\"privacySub\":\"Everything this tab can send\",\"testerSub\":\"Test builds and a checklist\",\"testerSubOn\":\"{done} of {count} steps tried\",\"support\":\"Support\",\"discord\":\"Discord\",\"qr\":{\"title\":\"Sign in with your phone\",\"step1\":\"Scan the code with your phone's camera.\",\"step2\":\"Sign in with Discord on the page that opens.\",\"step3\":\"Check that the page shows {code}, then press Approve.\",\"noCamera\":\"No camera? Open frostdev.io/link on any device and type the code.\",\"waiting\":\"Waiting for your phone. The code works for {left}.\",\"expired\":\"This code has expired.\",\"denied\":\"Declined on your phone. Nothing changed.\",\"newCode\":\"New code\",\"privacy\":\"What it can do is on frostdev.io/kiln/privacy.\"},\"joinQr\":{\"title\":\"Join the Kiln Discord\",\"text\":\"Scan the code with your phone to open the invite in Discord.\"},\"postQr\":\"Scan to open the post on your phone.\",\"privacyQr\":\"Scan the code to read the privacy page on your phone.\",\"when\":{\"title\":\"When does it happen?\",\"start\":\"when I press Play\",\"menu\":\"on the main menu\",\"load\":\"when I load a world\",\"play\":\"while I play\",\"join\":\"when I join a server\",\"another\":\"at another time\"},\"titles\":{\"noload\":\"Kiln doesn't load\",\"mod\":\"A mod doesn't work\",\"crash\":\"The game crashes\",\"launcher\":\"The launcher has a problem\",\"server\":\"My server has a problem\",\"something\":\"Something is wrong\"},\"what\":\"What went wrong?\",\"check\":\"Check and send\",\"type\":\"Type\",\"edit\":\"Change\",\"typeTitle\":\"Type your words\",\"typeHint\":\"Steam's keyboard opens by itself. If it doesn't, press Steam and X.\",\"wordsNone\":\"None. Optional.\",\"zipRow\":\"Report zip\",\"whatIsSent\":\"What is sent\",\"next\":\"Next\",\"pick\":\"Pick\",\"toggle\":\"Change\",\"done\":\"Done\",\"holdDelete\":\"Hold to delete my post\",\"postSay\":\"{status}. {replies}.\",\"noteWhy\":{\"title\":\"What happened?\",\"crashed\":\"The game crashed\",\"wrong\":\"It looked wrong\",\"slow\":\"It was slow or stuttered\",\"missing\":\"I couldn't find the option\",\"type\":\"Type a line\"}},\"settings\":{\"show\":\"Show the Community page\",\"showHint\":\"Kiln's Discord in the launcher. Off hides the page, its Deck tab and every Send to Kiln support, and the launcher stops everything it does with Discord.\"}}"),
	crucible: /* @__PURE__ */ JSON.parse("{\"checks\":{\"running\":\"Checking the pack…\",\"couldNot\":\"The pack couldn't be checked\",\"passed\":\"Checks: passed\",\"failed.one\":\"{count} problem stops the install\",\"failed.other\":\"{count} problems stop the install\",\"warnings.one\":\"{count} thing to look at\",\"warnings.other\":\"{count} things to look at\",\"failedBlock\":\"The pack failed its checks: it can't be installed.\",\"pending\":\"Checks: still running on Crucible\",\"lineWarnings.one\":\"Checks: {count} thing to look at\",\"lineWarnings.other\":\"Checks: {count} things to look at\",\"lineProblems.one\":\"Checks: {count} problem found\",\"lineProblems.other\":\"Checks: {count} problems found\",\"lineFailed\":\"Checks: couldn't run on Crucible\"},\"banner\":{\"title\":\"Pack update\",\"head\":\"{name} {change}\",\"headBy\":\"{name} {change} by {by}\",\"line\":\"{head}: {changes}.\",\"ready\":\"{head}: an update is ready.\",\"seeChanges\":\"See changes\"},\"change\":{\"added.one\":\"{count} added\",\"added.other\":\"{count} added\",\"updated.one\":\"{count} updated\",\"updated.other\":\"{count} updated\",\"removed.one\":\"{count} removed\",\"removed.other\":\"{count} removed\",\"addedWord.one\":\"{count} added\",\"addedWord.other\":\"{count} added\",\"updatedWord.one\":\"{count} updated\",\"updatedWord.other\":\"{count} updated\",\"removedWord.one\":\"{count} removed\",\"removedWord.other\":\"{count} removed\"},\"code\":{\"link\":\"Got a code?\",\"title\":\"Open a pack by its code\",\"hint\":\"Paste the code or link your friend sent.\",\"paste\":\"Paste\",\"pasteNone\":\"There's no pack code on the clipboard.\"},\"err\":{\"notFound\":\"We couldn't find that code. Codes look like H7QK-3MZP.\",\"offline\":\"Kiln can't reach Crucible. Ask your friend for the pack file (Share to Crucible > Save as file).\",\"gone\":\"This pack is no longer on Crucible.\",\"badDownload\":\"The pack didn't download correctly. Nothing was installed. Try again.\",\"gameRunning\":\"Close Enshrouded first.\",\"offlineShare\":\"Kiln can't reach Crucible. Check your internet and try again, or use Save as file and send your friends the file.\"},\"install\":{\"done\":\"{name} is installed\",\"doneDetail\":\"It's your loaded profile now. Your other profiles stay as they are.\",\"failed\":\"{name} was not installed\",\"backTo\":\"Back to {name}\",\"backToNone\":\"Back to no profile\",\"backDetail\":\"{name} stays in Settings > Profiles.\",\"undoFailed\":\"Couldn't go back\",\"profileName\":\"{pack} ({creator}'s pack)\",\"doneOnly\":\"It's in your profiles. Load it when you want to play.\"},\"preview\":{\"title\":\"Pack\",\"by\":\"by {name}\",\"fromFile\":\"from a file\",\"versionN\":\"v{version} (#{number})\",\"upvotes\":\"Upvotes\",\"fits\":\"Fits your game (build {build})\",\"fitsAny\":\"Fits your game\",\"otherBuild\":\"Made for game build {build}. You have {current}. Some mods may need updates.\",\"download\":\"Download {size}\",\"downloadSome.one\":\"Download {size}. {count} mod is already in your Library.\",\"downloadSome.other\":\"Download {size}. {count} mods are already in your Library.\",\"allInLibrary.one\":\"Nothing to download: the mod is already in your Library.\",\"allInLibrary.other\":\"Nothing to download: all {count} mods are already in your Library.\",\"code\":\"Code {code}\",\"kilnOldTitle\":\"Kiln is too old for this pack\",\"kilnOld\":\"Needs Kiln {version} or newer.\",\"updateKiln\":\"Update Kiln\",\"uploadingTitle\":\"Still uploading\",\"stillUploading\":\"{name}'s pack is still uploading ({pct}%). Wait here, and Install opens up when it's ready.\",\"resumed\":\"Kiln is ready. Here is {name}, the pack you came for.\",\"wait\":\"Wait\",\"waiting\":\"Waiting for {name}'s upload ({pct}%)…\",\"ownProfile\":\"Installs as its own profile. Your current mods stay as they are.\",\"have\":\"You have this pack already.\",\"haveUpdate\":\"You have this pack. Version {version} is ready.\",\"loadIt\":\"Load it\",\"loaded\":\"{name} is loaded\",\"optional\":\"Optional mods\",\"seeMods.one\":\"See the mod and its credit\",\"seeMods.other\":\"See the {count} mods and their credits\",\"hideMods\":\"Hide the mods\",\"modBy\":\"by {name}\",\"codeTag\":\"code\",\"codeTip\":\"Runs code on your PC\",\"optionalTag\":\"optional\",\"newTag\":\"new\",\"inLibrary\":\"in your Library\",\"warning\":\"This pack contains mods, including code, from other people. Crucible checks packs automatically but does not review them.\",\"trust\":\"I trust {name}'s pack\",\"trustFile\":\"I trust this pack\",\"trustAlways\":\"Always trust packs from {name}\",\"trustedAlready\":\"You trust packs from {name}.\",\"tickFirst\":\"Tick \\\"I trust\\\" first.\",\"installPlay\":\"Install and play\",\"cantOpen\":\"Can't open this pack\",\"noGame\":\"Kiln hasn't found Enshrouded yet.\",\"installKiln\":\"Install Kiln in the game first.\",\"nameLabel\":\"Profile name\",\"nameTaken\":\"You have a profile with this name. This one will be {name}.\",\"runningHint\":\"Enshrouded is running. Install adds the pack now; load it once you close the game.\",\"installOnlyTip\":\"Adds the pack as its own profile. Doesn't load it or start the game.\",\"warningFile\":\"This pack file contains mods, including code, from other people. Kiln checked the file, but nobody has reviewed it.\"},\"profiles\":{\"tag\":\"Crucible pack\",\"line\":\"{name} {version} · {ago}\",\"updateReady\":\"{version} is ready\",\"seeUpdate\":\"See the update\",\"undoUpdate\":\"Undo last update (back to {version})\",\"uninstall\":\"Uninstall\",\"uninstallAsk\":\"Uninstall {name}?\",\"uninstallText\":\"The profile goes away. Its mods stay in your Library, and your other profiles stay as they are.\",\"uninstalled\":\"{name} is uninstalled\",\"unused.one\":\"{count} mod is no longer used: Library > Clean up removes it.\",\"unused.other\":\"{count} mods are no longer used: Library > Clean up removes them.\",\"cardTitle\":\"Crucible\",\"cardSub\":\"Packs you installed and packs you shared.\",\"installed\":\"Installed packs\",\"shared\":\"Packs you shared\",\"asProfile\":\"as the profile {name}\",\"updateTo\":\"Update to {version}\",\"noInstalled\":\"No packs yet. Got a code from a friend? Enter it on Play.\",\"noShared\":\"Nothing shared yet. Share to Crucible on Play turns your mods into a link.\",\"sharedVersion\":\"version {number}\"},\"repair\":{\"button.one\":\"Download the missing mod ({size})\",\"button.other\":\"Download missing mods ({count}, {size})\",\"fromFile.one\":\"Add the missing mod from this file\",\"fromFile.other\":\"Add the {count} missing mods from this file\",\"title.one\":\"A mod of this pack is missing\",\"title.other\":\"{count} mods of this pack are missing\",\"missing.one\":\"{names} (pack version {version}) is no longer in your Library. Load it would start the game without it.\",\"missing.other\":\"{names} (pack version {version}) are no longer in your Library. Load it would start the game without them.\",\"missingLayer\":\"The pack's own settings (pack version {version}) are no longer in your Library. Load it would start the game without them.\",\"andLayer\":\"The pack's own settings are missing too.\",\"needFile\":\"Open the file of version {version} to put them back.\",\"running\":\"Downloading the missing mods…\",\"done.one\":\"{name}: the missing mod is back in your Library\",\"done.other\":\"{name}: {count} mods are back in your Library\",\"doneDetail\":\"Downloaded {size}. Nothing else changed: load the profile when you want to play.\",\"doneLocal\":\"Nothing else changed: load the profile when you want to play.\",\"nothing\":\"{name}: nothing is missing\",\"partly\":\"{name}: {count} back in your Library, some still missing\",\"left.one\":\"{names} is in your Library but its files were changed: Library shows how to take the change over or put the old files back.\",\"left.other\":\"{names} are in your Library but their files were changed: Library shows how to take the change over or put the old files back.\",\"failed\":\"The missing mods of {name} were not downloaded\"},\"save\":{\"button\":\"Save as file\",\"pick\":\"Where to save the pack file\",\"done\":\"Saved {name} as a file\",\"failed\":\"{name} was not saved\",\"openFolder\":\"Open folder\"},\"settings\":{\"stats\":\"Count my installs (anonymous)\",\"statsHint\":\"Crucible counts installs per day for each pack. Nothing about you is sent.\"},\"share\":{\"button\":\"Share to Crucible\",\"uploadingShort\":\"Uploading…\",\"changedPill\":\"Pack changed\",\"signInText\":\"Sign in with Discord to update this account-owned share. Your friends don't need to.\",\"noAccount\":\"No Discord login is needed to share an unlisted link or install it. Anyone with the link or code can install. Keep this device's launcher data to update the same link.\",\"signIn\":\"Sign in with Discord\",\"saveInstead\":\"Save as file instead\",\"uploadingTitle\":\"Sharing {name}\",\"starting\":\"Getting ready…\",\"keepsGoing\":\"You can close this. The upload keeps going.\",\"copied\":\"Link copied. Paste it to your friends.\",\"ready\":\"Your link is ready\",\"pasteIt\":\"Your friends click it, then Install and play.\",\"copyIt\":\"Copy the link and paste it to your friends.\",\"sharedTitle\":\"{name} is shared\",\"changedTitle\":\"Your pack changed\",\"updateAsk\":\"Update the link for friends? {changes}\",\"updateLink\":\"Update link\",\"codeLabel\":\"Code\",\"linkCopied\":\"Link copied\",\"codeCopied\":\"Code copied\",\"copyLink\":\"Copy link\",\"noClipboard\":\"Copying isn't possible here. Select the text instead.\",\"failed\":\"Sharing didn't work\",\"versionLine\":\"Version {number}\",\"what\":\"What is Crucible?\",\"confirmTitle\":\"Share {name} to Crucible?\",\"confirmWhat\":\"Crucible turns this profile into a link. Friends open it in Kiln and get the same mods.\",\"confirmGoesSize.one\":\"Goes up: the mod list, and the files of its {count} mod that Crucible doesn't have yet (up to {size}).\",\"confirmGoesSize.other\":\"Goes up: the mod list, and the files of its {count} mods that Crucible doesn't have yet (up to {size}).\",\"confirmGoes.one\":\"Goes up: the mod list, and the files of its {count} mod that Crucible doesn't have yet.\",\"confirmGoes.other\":\"Goes up: the mod list, and the files of its {count} mods that Crucible doesn't have yet.\",\"confirmGoesAny\":\"Goes up: the mod list, and the mods' files that Crucible doesn't have yet.\",\"confirmStays\":\"Stays on this PC: your mod settings and your own data edits.\",\"confirmNever\":\"Never sent: your saves, worlds, characters or any other personal file.\",\"visibility\":\"Who can open it\",\"linkOnly\":\"Link only:\",\"linkOnlyText\":\"not listed in the gallery. Anyone with the link or code can install.\",\"public\":\"Public:\",\"publicText\":\"listed on Crucible for everyone. Public packs are made in Pack Studio (they need credits and a summary).\",\"confirmShare\":\"Share\",\"confirmSignIn\":\"Sign in and share\"},\"update\":{\"title\":\"{name}: {from} → {to}\",\"titleLoading\":\"Updating {name}\",\"getting\":\"Getting the update ready…\",\"cantRead\":\"Can't read the update\",\"removedKept\":\"{name} (kept in your Library)\",\"packData\":\"The pack's own data changed\",\"renamed\":\"{from} is {to} now: your settings move with it.\",\"stillWins.one\":\"Your change still wins ({count})\",\"stillWins.other\":\"Your changes still win ({count})\",\"dataChanged\":\"The pack now sets {pack}. Yours, {mine}, still counts.\",\"dataSame\":\"The pack sets {pack}. Yours, {mine}, still counts.\",\"dropMine\":\"Drop my change (use {value})\",\"savesFirst\":\"Your saves are backed up first.\",\"download\":\"Download {size}\",\"needsYou.one\":\"Needs you ({count})\",\"needsYou.other\":\"Needs you ({count})\",\"both\":\"The pack now says {pack}. You had {mine}.\",\"takePack\":\"Take {value}\",\"keepMine\":\"Keep {value}\",\"notNow\":\"Not now\",\"update\":\"Update\",\"updatePlay\":\"Update and play\",\"chooseFirst\":\"Choose for each setting first.\",\"needsChoice\":\"A setting needs your choice.\",\"done\":\"{name} is up to date ({version})\",\"doneDetail\":\"Your own changes stayed.\",\"failed\":\"{name} was not updated\",\"undone\":\"{name} is back to {version}\",\"undoFailed\":\"Couldn't undo the update\",\"keptLine\":\"Your changes kept: {list}.\",\"keptSettings\":{\"one\":\"{count} setting\",\"other\":\"{count} settings\"},\"keptData\":{\"one\":\"{count} data edit\",\"other\":\"{count} data edits\"},\"keptMods\":{\"one\":\"{count} mod\",\"other\":\"{count} mods\"}},\"deck\":{\"shareTitle\":\"Share {name}\",\"exportTitle\":\"Save {name} as a file\",\"exportLocal\":\"Save a local .kilnpack file. Nothing is uploaded, and no account or internet is needed.\",\"exportDestination\":\"Save in this folder?\",\"exportFinding\":\"Finding Downloads or your home folder…\",\"exportNoFolder\":\"Kiln couldn't find a safe Downloads or home folder. Nothing was written. Check the folder and try again.\",\"exportReplace\":\"Saving replaces any file for this pack at version 1.0.0 in this folder.\",\"exportSaving\":\"Saving and checking the pack…\",\"exportLocation\":\"Saved file location:\",\"exportCanceled\":\"Export canceled.\",\"scan\":\"Friends scan the code with a phone, or type the code in Kiln.\",\"enterCode\":\"Enter a code\",\"codeHint\":\"8 letters and numbers, like H7QK-3MZP.\",\"installing\":\"Installing…\",\"loading\":\"Opening the pack…\",\"tick\":\"Tick\",\"untick\":\"Untick\",\"turnOn\":\"Turn on\",\"turnOff\":\"Turn off\"}}"),
	studio: /* @__PURE__ */ JSON.parse("{\"nav\":\"Studio\",\"title\":\"Pack Studio\",\"sub\":\"Make Crucible packs from your profiles: mods, settings, your own data, credits, then publish.\",\"new\":\"New pack\",\"newFrom\":\"Start from\",\"newEmpty\":\"Empty (a new profile)\",\"newProfile\":\"{name} ({count} mods)\",\"newName\":\"Name\",\"newNamePh\":\"My pack\",\"create\":\"Create\",\"newFailed\":\"The pack was not made\",\"empty\":{\"title\":\"No packs yet\",\"hint\":\"Start one from a profile you play: its mods and settings come along.\"},\"packLine\":{\"one\":\"v{version} · {count} mod · changed {ago}\",\"other\":\"v{version} · {count} mods · changed {ago}\"},\"packSub\":{\"one\":\"Version {version}, made from the profile {profile} ({count} mod).\",\"other\":\"Version {version}, made from the profile {profile} ({count} mods).\"},\"profileGone\":\"The profile this pack was made from is gone.\",\"missing\":\"This pack is not here\",\"back\":\"All packs\",\"allPacks\":\"All packs\",\"tabsAria\":\"Pack Studio\",\"deleteTitle\":\"Delete {name}?\",\"deleteText\":\"The pack's draft goes to Kiln's backups. Its profile stays, and what you published stays on Crucible.\",\"recheck\":\"Check again\",\"state\":{\"draft\":\"Draft\",\"link\":\"Link only\",\"public\":\"Public\"},\"tab\":{\"mods\":\"Mods\",\"settings\":\"Settings\",\"data\":\"Data\",\"order\":\"Load order\",\"test\":\"Test\",\"credits\":\"Credits\",\"publish\":\"Publish\"},\"mods\":{\"lead\":\"The mods of the profile {profile}, each as your Library holds it. Players switch optional mods on or off before they install.\",\"addLibrary\":\"Add from my Library\",\"addFile\":\"Add file\",\"pickFile\":\"Add a mod file to the pack\",\"addFailed\":\"The mod was not added\",\"removeFailed\":\"{name} was not removed\",\"empty\":\"No mods yet. Add some from your Library.\",\"mod\":\"Mod\",\"version\":\"Version\",\"need\":\"In the pack\",\"sides\":\"Runs on\",\"source\":\"Source\",\"needAria\":\"How {name} is in the pack\",\"sidesAria\":\"Where {name} runs\",\"required\":\"Required\",\"optionalOn\":\"Optional, on\",\"optionalOff\":\"Optional, off\",\"both\":\"Players and servers\",\"client\":\"Players only\",\"server\":\"Servers only\",\"checking\":\"Checking…\",\"unknown\":\"Not identified\",\"filter\":\"Filter\",\"libraryEmpty\":\"Every mod of your Library is in the pack already.\",\"addN\":{\"one\":\"Add {count} mod\",\"other\":\"Add {count} mods\"}},\"settings\":{\"lead\":\"Settings travel as the keys you choose, taken from the profile as you play it. Keys that match a mod's default are left out, and so are hotkeys unless you tick them. Anything that looks private never travels.\",\"capture\":\"Capture from my current settings\",\"captureFailed\":\"Kiln couldn't read the profile's settings\",\"save\":{\"one\":\"Use {count} key\",\"other\":\"Use {count} keys\"},\"saved\":{\"one\":\"{count} settings file is in the pack\",\"other\":\"{count} settings files are in the pack\"},\"saveFailed\":\"The settings were not taken\",\"none\":\"This profile has no settings files yet. Play it once, change what you like, then capture.\",\"refused\":\"can't travel: {why}\",\"wholeOnly\":\"travels as a whole file\",\"differs\":{\"one\":\"differs from the mod's defaults in {count} key\",\"other\":\"differs from the mod's defaults in {count} keys\"},\"noDefaults\":{\"one\":\"{count} key (no defaults known)\",\"other\":\"{count} keys (no defaults known)\"},\"sideAria\":\"Who gets {file}\",\"whole\":\"Whole file\",\"default\":\"(default {value})\",\"personal\":\"personal\",\"secret\":\"private\"},\"server\":{\"title\":\"Server game settings\",\"lead\":\"Game settings a server of this pack uses (health, enemies, building). Taken from one of your servers; its name, password and ports never travel.\",\"none\":\"Add a server in Servers to take its game settings.\",\"pick\":\"Server\",\"take\":\"Take from this server\",\"takeFailed\":\"Kiln couldn't read the server's settings\",\"use\":\"Use the ticked settings\",\"saved\":\"The server game settings are in the pack\",\"clear\":\"Remove them\"},\"data\":{\"tabsAria\":\"Data editors\",\"tab\":{\"items\":\"Items\",\"recipes\":\"Recipes\",\"loot\":\"Loot\",\"drops\":\"Drops\",\"added\":\"New items\",\"raw\":\"Raw JSON\"},\"lint\":{\"line\":\"Data check: {errors}, {warnings}\",\"errors\":{\"one\":\"{count} error\",\"other\":\"{count} errors\"},\"warnings\":{\"one\":\"{count} warning\",\"other\":\"{count} warnings\"},\"checking\":\"Data check: checking…\",\"show\":\"Show problems\",\"hide\":\"Hide problems\",\"error\":\"Error\",\"warning\":\"Warning\",\"info\":\"Note\",\"lineNotes\":\"Data check: {errors}, {warnings}, {notes}\",\"notes\":{\"one\":\"{count} note\",\"other\":\"{count} notes\"}},\"noCatalog\":\"Kiln hasn't read the game's items yet, so the pickers only list what the pack's mods add and you type field names by hand.\",\"catalog\":{\"reading\":\"Reading the game's items…\",\"build\":\"Build the game catalog\",\"why\":{\"noGame\":\"No Enshrouded client found. Add it in Settings → Games.\",\"steamUpdating\":\"Steam is updating the game. Try again when it is done.\",\"conflict\":\"The game's files are from two different versions (Steam may be in the middle of an update). Try again when Steam is done.\",\"failed\":\"Kiln could not read the game's items: {message}\"}},\"thisPack\":\"this pack\",\"save\":\"Save\",\"saved\":\"Saved\",\"unsaved\":\"Unsaved changes\",\"discard\":\"Discard\",\"remove\":\"Remove\",\"saveFailed\":\"{file} was not saved\",\"broken\":\"This file is not valid JSON. Fix it in Raw JSON first.\",\"alsoIn\":\"Also set in {files} (Raw JSON).\",\"name\":\"Name\",\"guid\":\"Fixed GUID (made once, never change it)\",\"badName\":\"A name is 1 to 200 letters, digits, _ - or .\",\"dupName\":\"{name} is used twice\",\"pick\":{\"ph\":\"Search items\",\"selectorPh\":\"Name or pattern like Material_T1_*\",\"glob\":\"every match\",\"unknown\":\"not found in the game or the pack's mods\",\"none\":\"Nothing found\",\"searching\":\"Searching…\"},\"value\":{\"set\":\"set to\",\"mul\":\"times\",\"add\":\"plus\",\"min\":\"min\",\"max\":\"max\",\"modeAria\":\"How {field} changes\",\"minAria\":\"Lowest {field}\",\"maxAria\":\"Highest {field}\",\"raiseOnly\":\"raise only\",\"lowerOnly\":\"lower only\",\"on\":\"on\",\"off\":\"off\"},\"field\":{\"add\":\"+ Field\",\"remove\":\"Remove {field}\",\"nameAria\":\"Field\"},\"where\":{\"add\":\"+ Condition\",\"where\":\"where\",\"and\":\"and\",\"fieldAria\":\"Condition field\",\"fieldPh\":\"field\",\"valueAria\":\"Condition on {field}\",\"remove\":\"Remove condition\"},\"rules\":{\"add\":\"+ Rule\",\"addAll\":\"+ Rule for every item\",\"remove\":\"Remove rule\",\"selectorAria\":\"Which entries\",\"noSelector\":\"Every rule needs a name or a pattern\",\"duplicate\":\"{selector} has two rules: merge them into one\"},\"items\":{\"lead\":\"Change the game's items and the pack's mods' items: a rule per item or pattern, with conditions on the game's values. The pack layer loads after every mod, so its values win.\",\"empty\":\"No item rules yet.\",\"selector\":\"Items\"},\"loot\":{\"lead\":\"Random loot of chests and world pickups, per item: how many come in a stack.\",\"empty\":\"No loot rules yet.\",\"selector\":\"Item\"},\"recipes\":{\"lead\":\"Change what recipes take and make, and how long they take, or add recipes of your own.\",\"changes\":\"Changed recipes\",\"noChanges\":\"No recipe changes yet.\",\"add\":\"+ Change a recipe\",\"recipe\":\"Recipe\",\"noRecipe\":\"Every change needs a recipe\",\"changeInputs\":\"Change the inputs\",\"changeOutputs\":\"Change the outputs\",\"inputs\":\"Inputs\",\"outputs\":\"Outputs\",\"addInput\":\"+ Input\",\"addOutput\":\"+ Output\",\"duration\":\"Duration\",\"durationPh\":\"game's\",\"seconds\":\"seconds\",\"water\":\"Water from the tank\",\"waterTip\":\"The recipe's own water, in charges a craft (64 water units each), as the recipe screen's Water row shows it. The game takes it when a craft starts.\",\"waterPh\":\"none\",\"waterPhBase\":\"base's\",\"waterPhGame\":\"game's\",\"waterUnit\":\"charges a craft\",\"waterTank\":\"The {station}'s tank holds {max} charges: keep it filled to craft.\",\"waterOver\":\"More than the {station}'s tank holds ({max}): the station never starts this recipe.\",\"waterNoTank\":\"The {station} has no water tank: it never starts a recipe that needs water.\",\"waterMenu\":\"Crafted from a menu: the game takes the water from the player's water containers and the base's wells and barrels.\",\"oldWater\":\"An input is the game's old Water item, a canteen: each craft takes {count} of that item, not water from the station's tank.\",\"useTank\":\"Use tank water instead\",\"useTankTip\":\"Removes the Water item inputs and asks for the same amount of tank water\",\"itemAria\":\"Item\",\"countAria\":\"Count\",\"category\":\"any item of category {id}\",\"addCategory\":\"+ Any of a group\",\"addCategoryTip\":\"An input the player can pay with any item of a group, such as any wood\",\"anyOf\":\"Any of: {names}\",\"anyOfMore\":\"Any of: {names}, +{count} more\",\"categoryAria\":\"Group of items\",\"categoryPh\":\"Search by any item in it\",\"categoryNone\":\"No group holds that item\",\"categoryItems\":{\"one\":\"{count} item\",\"other\":\"{count} items\"},\"categoryUsed\":{\"one\":\"used by {count} game recipe\",\"other\":\"used by {count} game recipes\"},\"categoryUnused\":\"No game recipe uses it\",\"categoryUsedBy\":\"Game recipes that take it: {names}\",\"categoryUsedByMore\":\"Game recipes that take it: {names}, +{count} more\",\"categoryShow\":\"Show its items\",\"categoryHide\":\"Hide its items\",\"categoryNoCatalog\":\"Needs the game's items: Kiln reads them first\",\"removeCategory\":\"Remove this group\",\"offer\":\"Accept any of its group?\",\"offerAria\":\"Accept any of its group instead ({anyOf})\",\"modNote\":\"Changes the mod's recipe; the pack layer wins (it loads after {mod}).\",\"stationNote\":\"A recipe of the game keeps its station; new recipes choose their own.\",\"new\":\"New recipes\",\"noNew\":\"No new recipes yet.\",\"addNew\":\"+ New recipe\",\"removeNew\":\"Remove new recipe\",\"station\":\"Station\",\"stationPh\":\"Search stations\",\"hand\":\"Crafted by hand\",\"noStation\":\"Every new recipe needs a station\",\"needStacks\":\"Every new recipe needs an input and an output\",\"noCategory\":\"Choose a group for every \\\"Any of\\\" input\"},\"drops\":{\"lead\":\"Default inventories: enemy loot and fixed chests, by table or for every table (*).\",\"empty\":\"No drop rules yet.\",\"table\":\"Table\",\"add\":\"+ Table\",\"addAll\":\"+ Rule for every table\",\"count\":\"Amounts\",\"chance\":\"Chance\",\"items\":\"Single items\",\"addItem\":\"+ Item\",\"noTable\":\"Every drop rule needs a table\"},\"added\":{\"lead\":\"Items of your own: each is a copy of a base item (its model, icon and behaviour) with its own name and values.\",\"empty\":\"No new items yet.\",\"add\":\"+ New item\",\"remove\":\"Remove new item\",\"displayName\":\"Shown as\",\"base\":\"Base item\",\"description\":\"Description\",\"rolledLoot\":\"Rolled like loot (rarity and level vary)\",\"fields\":\"Values\",\"noBase\":\"Every new item needs a base item\"},\"raw\":{\"none\":\"No files yet.\",\"newPh\":\"name.json\",\"newAria\":\"New file name\",\"new\":\"New file\",\"rename\":\"Rename\",\"renameAria\":\"New name\",\"renameOk\":\"Rename\",\"exists\":\"{file} exists already\",\"delete\":\"Delete\",\"deleteTitle\":\"Delete {file}?\",\"deleteText\":\"The file leaves the pack layer.\",\"textAria\":\"Text of {file}\",\"clean\":\"No problems in this file.\",\"pick\":\"Pick a file or make a new one.\"},\"hidden\":{\"toggle\":\"Show hidden game entries\",\"tag\":\"hidden\",\"tip\":\"Old, test or unused game entries: they may do nothing in the game.\",\"oldWater\":\"old canteen item, not tank water\",\"oldWaterTip\":\"The game's old Water item. The recipe screen uses its name for a recipe's water, but as an ingredient it is the item itself. For tank water, use Water from the tank on the recipe.\"}},\"order\":{\"title\":\"Load order\",\"always\":\"(the pack layer, always last)\",\"late\":\"Late native DLLs: {list}\",\"conflicts\":\"Conflicts\",\"none\":\"No two mods set the same field differently.\",\"noCatalog\":\"Without the game-data catalog, conflicts of wildcards are only guesses.\",\"maybe\":\"maybe\",\"wins\":\"wins today\",\"setIn\":\"Set in the pack:\",\"set\":\"Set\",\"setAria\":\"Value of {field} in the pack\",\"setDone\":\"{field} is set in the pack layer\"},\"test\":{\"lead\":\"Try the pack exactly as players get it: Kiln builds it and installs it as a profile of its own.\",\"backup\":\"Your saves are backed up first.\",\"world\":\"Start a new world for the test to keep your own worlds as they are.\",\"server\":\"For a server pack, put a published version on one of your servers (Publish).\",\"make\":\"Test this pack\",\"again\":\"Test again\",\"play\":\"Play the test\",\"end\":\"End the test\",\"failed\":\"The test instance was not made\",\"ready\":\"{name} is ready\",\"readyDetail\":\"Press Play to start it.\",\"noGame\":\"Add the game first.\",\"reported\":\"What the game reported\",\"notYet\":\"Play the test, then come back here.\",\"clean\":\"No conflicts or errors in the last run.\",\"conflicts\":{\"one\":\"{count} data conflict\",\"other\":\"{count} data conflicts\"},\"problems\":{\"one\":\"{count} error or warning\",\"other\":\"{count} errors or warnings\"}},\"credits\":{\"lead\":\"Public packs credit every mod: its author and an https link to where it comes from. Kiln fills in what it recognises; check each row.\",\"author\":\"Author\",\"source\":\"Source\",\"matched\":\"Found by\",\"authorAria\":\"Author of {name}\",\"sourceAria\":\"Where {name} comes from\",\"notIdentified\":\"Not identified: fill in or remove the mod\",\"missing\":{\"one\":\"{count} mod still needs a credit for a public pack\",\"other\":\"{count} mods still need a credit for a public pack\"},\"complete\":\"Every mod is credited\",\"save\":\"Save credits\",\"saved\":\"The credits are saved\",\"saveFailed\":\"The credits were not saved\"},\"match\":{\"kiln\":\"Kiln's own\",\"crucible\":\"Crucible\",\"github\":\"GitHub release\",\"nexus\":\"Nexus\",\"manifest\":\"the mod's own info\",\"manual\":\"you\"},\"confidence\":{\"certain\":\"certain\",\"likely\":\"likely\",\"conflict\":\"differs from an earlier credit\",\"unverified\":\"unverified\",\"creator\":\"yours\",\"unconfirmed\":\"unconfirmed\",\"none\":\"none\"},\"publish\":{\"signInTitle\":\"Sign in to publish\",\"signIn\":\"Publishing uses your Discord sign-in. Players don't need one to install.\",\"version\":\"Version\",\"changes\":\"What changed\",\"changesPh\":\"Rebalanced loot; added Busy Hands\",\"tested\":\"Tested with game build {build}\",\"visibility\":\"Visibility\",\"public\":\"Public\",\"link\":\"Link only\",\"summary\":\"Summary\",\"summaryHint\":\"One line for Crucible's lists (at least 10 characters).\",\"attest\":\"I have credited every mod and its author, and I may share these files.\",\"checks\":\"Checks\",\"check\":{\"format\":\"format\",\"sizes\":\"sizes\",\"blocked\":\"known bad files\",\"code\":{\"one\":\"code scan ({count} note)\",\"other\":\"code scan ({count} notes)\"},\"lint\":{\"one\":\"data check ({count} warning)\",\"other\":\"data check ({count} warnings)\"}},\"tone\":{\"ok\":\"ok\",\"warn\":\"look\",\"err\":\"error\",\"note\":\"note\"},\"go\":\"Publish\",\"saveFile\":\"Save as file\",\"failed\":\"The pack was not published\",\"donePublic\":\"Version {version} is public\",\"doneLink\":\"Version {version} is published\",\"copied\":\"Link copied.\",\"isPublic\":\"Public on Crucible\",\"isLink\":\"Published, link only\",\"history\":\"Published versions\",\"toServer\":\"Put on a server\",\"makePublic\":\"Make public\",\"makeLink\":\"Make it link only\",\"goFresh\":\"Publish as a new pack\",\"goneTitle\":\"This pack is no longer on Crucible\",\"gone\":\"It was deleted or taken down there. Publishing makes a new pack with a new link and code.\",\"notYetPublic\":\"Not public yet\"},\"serverPack\":{\"title\":\"{name} {version} on a server\",\"lead\":\"The version's server mods, its server settings and its game settings go onto the server, and the modpack players download is built from it. Stop the server first.\",\"go\":\"Put on the server\",\"done\":\"{version} is on the server\",\"failed\":\"The server pack was not made\"}}"),
	editor: /* @__PURE__ */ JSON.parse("{\"page\":{\"title\":\"Editor\",\"lookingForSaves\":\"Looking for saves…\",\"pickCharacter\":\"Pick a character.\",\"noSaves\":\"No character saves found.\",\"cannotParse\":\"This save could not be parsed for editing.\",\"readOnlyNoSave\":\"This save can only be read: Save is off.\",\"gameOpen\":\"The game is open: close it to save.\",\"unsaved\":\"Changes not saved yet. Kiln backs the save up before it writes.\",\"levelBackup\":\"Level {n}. Kiln backs the save up before it writes.\",\"notSaved\":\"Not saved\",\"history\":{\"undo\":\"Undo\",\"redo\":\"Redo\",\"emptyUndo\":\"Nothing to undo since the last save\",\"emptyRedo\":\"Nothing to redo since the last save\",\"undoAction\":\"Undo: {action}\",\"redoAction\":\"Redo: {action}\",\"change\":\"Edit character\",\"discard\":\"Discard changes\",\"changeLevel\":\"Change level\",\"changeXp\":\"Change XP\",\"changeSkills\":\"Change skills\",\"changeAmount\":\"Change amount\",\"changeItem\":\"Change item\",\"moveItems\":\"Move items\",\"removeItems\":\"Remove items ({count})\",\"addItems\":\"Add items ({count})\",\"removeKnowledge\":\"Remove knowledge ({count})\",\"changeKnowledge\":\"Change knowledge\"},\"discard\":\"Discard\",\"backups\":\"Backups\",\"save\":\"Save\",\"saving\":\"Saving…\",\"opening\":\"Opening character…\",\"saved\":\"Saved\",\"savedHint\":\"The save before it is in Backups.\",\"gameOpenShort\":\"Game running: close it to save\",\"error\":\"Error\",\"note\":\"Note\",\"readAgain\":\"Read again\",\"close\":\"Close\",\"parts\":\"Parts of the save\",\"characters\":\"Characters\",\"openFile\":\"Open file…\",\"pickCharacterButton\":\"Pick a character\",\"open\":\"Open\",\"opened\":\"Open\",\"discardTitle\":\"Discard the changes?\",\"discardAndOpen\":\"Discard and open\",\"discardBody\":\"The changes to {nome} are not saved yet.\",\"cancel\":\"Cancel\",\"back\":\"Back\",\"readOnlyError\":\"Read-only: {erro}\",\"pointsRaised\":\"You raised the points in use ({antes} → {depois}). Enter your point total on the Skills tab so the editor can check it; above the total, the game resets the whole tree.\",\"notWritten\":\"Not saved. {problemas}\",\"gameWroteSince\":\"The game wrote this save after the editor read it. Read it again to edit the save as it is now (the changes made here are lost).\"},\"tabs\":{\"equipment\":\"Equipment\",\"inventory\":\"Backpack\",\"skills\":\"Skills\",\"knowledge\":\"Knowledge\"},\"backups\":{\"title\":\"Backups for {nome}\",\"openFolder\":\"Open folder\",\"hint\":\"The game keeps the last 10 saves automatically; the editor also copies the save before every write.\",\"dropsChanges\":\"Restoring drops the changes that are not saved.\",\"looking\":\"Looking for backups…\",\"none\":\"No backup found for this save.\",\"restore\":\"Restore\",\"restored\":\"Restored\",\"restoring\":\"Restoring…\",\"legacyTitle\":\"Older backups (origin unknown)\",\"legacyHint\":\"These backups may belong to another save. Check the character after restoring.\",\"restoredBody\":\"{arquivo} ({quando}). The save before it was kept as a backup.\",\"restoreTitle\":\"Restore this backup?\",\"restoreBody\":\"The save as it is now is kept as a backup.\",\"kindGame\":\"Game's copy\",\"kindKiln\":\"Kiln backup\",\"beforeSave\":\"before a save\"},\"common\":{\"level\":\"Level\",\"empty\":\"Empty\",\"change\":\"Change\",\"add\":\"Add\",\"tierN\":\"Tier {n}\",\"rarity\":\"Rarity\",\"amount\":\"Amount\",\"aboveStack\":\"more than the game's stack of {n}: for worlds with a mod that raises stack sizes\",\"gameStack\":\"Game's stack ({n})\",\"maxAmount\":\"Most a slot holds ({n})\",\"upgrades\":\"Upgrades\",\"remove\":\"Remove\",\"levelN\":\"level {n}\",\"pick\":\"Pick\",\"noMatch\":\"Nothing matches these filters.\",\"all\":\"All\"},\"equipment\":{\"xp\":\"XP in level\",\"attrsHint\":\"Attributes come from the skill tree nodes (edit them on the Skills tab).\",\"attrsFlameHint\":\"The game also adds the nodes that scale with your Flame level and the bonuses from your gear; neither is stored in the character save.\",\"emptyHint\":\"Empty slot. Equip the piece in game; here you can edit or take off what is already worn.\",\"backpackFull\":{\"one\":\"I will not take off the {item}: that would delete its container and the {count} slot stored in it. Empty it in game first.\",\"other\":\"I will not take off the {item}: that would delete its container and the {count} slots stored in it. Empty it in game first.\"},\"groups\":{\"armour\":\"Armour\",\"appearance\":\"Appearance\",\"accessories\":\"Accessories\"},\"slots\":{\"headVanity\":\"Head (vanity)\",\"chestVanity\":\"Chest (vanity)\",\"armsVanity\":\"Arms (vanity)\",\"legsVanity\":\"Legs (vanity)\",\"feetVanity\":\"Feet (vanity)\",\"head\":\"Head\",\"chest\":\"Chest\",\"arms\":\"Arms\",\"legs\":\"Legs\",\"feet\":\"Feet\",\"ring\":\"Ring\",\"shield\":\"Shield\",\"hook\":\"Grappling hook\",\"glider\":\"Glider\",\"backpack\":\"Backpack\",\"slot\":\"Slot {n}\"}},\"inventory\":{\"move\":\"Move\",\"slotEmpty\":\"Slot {n} empty\",\"putHere\":\"Put here\",\"slotsUsed\":{\"one\":\"{n} of {total} slot\",\"other\":\"{n} of {total} slots\"},\"addItem\":\"Add item\",\"swapBar\":\"Swap bar\",\"swapBarHint\":\"Alt swaps it with the action bar\",\"actionBar\":\"Action bar\",\"deckHint\":\"A changes the item · Y moves it · A on an empty slot adds one\",\"hint\":\"Drag or M to move · arrows select · Enter inspects/adds · Delete removes · Ctrl+D duplicates\",\"movingHint\":\"Moving {place}. Choose a destination, then Enter or Space to move or swap. Escape cancels.\",\"noneSelected\":\"No item selected.\",\"noneSelectedHint\":\"Pick a slot to see and change its item.\",\"emptyHint\":\"Pick an item from the catalog to put here.\",\"pickItem\":\"Pick item\",\"notFound\":\"No items were found in this inventory, and the editor cannot tell for sure where it sits in this save. If the game shows items here, they could not be read: nothing can be added. A copy of this save sent to us helps fix it.\",\"modItem\":\"Mod item, kept untouched\",\"hiddenModItem\":\"{place} holds a hidden item from another mod (id {id}); Kiln keeps it untouched.\",\"place\":{\"backpack\":\"Backpack slot {n}\",\"swap\":\"Swap bar slot {n}\",\"action\":\"Action bar slot {n}\"}},\"inspector\":{\"item\":\"Item\",\"stacksTo\":\"stacks up to {n}\",\"max\":\"Max\",\"levelSplit\":{\"one\":\"= base {base} + {count} upgrade\",\"other\":\"= base {base} + {count} upgrades\"},\"aboveItemMax\":\"above this item's maximum ({max}): the server clamps it on load, unless its range was opened up by a mod\",\"upgradesHint\":\"Rarity sets how many upgrades the item takes: max level = base level + rarity.\",\"invalidLevels\":\"Item levels and upgrades must be whole numbers from 0 to 255.\",\"gem\":\"Socketed gem\",\"gemUnknown\":\"id {id} is not in the catalog\",\"noGem\":\"— no gem —\",\"gemLevel\":\"Gem level\",\"aboveGemMax\":\"above this gem's maximum ({max}): the server clamps it on load, unless its range was opened up by a mod\",\"durability\":\"Durability\",\"repair\":\"Repair\",\"water\":\"Water\",\"fill\":\"Fill\",\"duplicateHint\":\"Copies the item into the first free slot\",\"duplicate\":\"Duplicate\",\"attributes\":\"Attributes\",\"physicalArmour\":\"Physical Armour\",\"magicalArmour\":\"Magical Armour\",\"raisesSkill\":\"raises the skill level of {no}\",\"unknownLines\":\"The lines without a number scale with the item level by a formula that has not been measured yet; the editor does not show a value it cannot stand behind.\"},\"parts\":{\"full\":\"Full\",\"clickToSet\":\"Click to set\"},\"catalog\":{\"items\":\"Items\",\"countLimited\":{\"one\":\"{count} item (showing {limite}; narrow the search)\",\"other\":\"{count} items (showing {limite}; narrow the search)\"},\"count\":{\"one\":\"{count} item\",\"other\":\"{count} items\"},\"categories\":\"Categories\",\"searchDeck\":\"Search by name (Steam + X: keyboard)\",\"search\":\"Search by name…\",\"searchLabel\":\"Search\",\"type\":\"Type\",\"allRarities\":\"All\",\"tier\":\"Tier\",\"targetFull\":\"Target: inventory full\",\"target\":\"Target: {place}\",\"pickHint\":\"Pick an item.\",\"deckHint\":\"A picks it, A again adds it.\",\"hint\":\"Double-click adds it at once.\",\"cat\":{\"weapons\":\"Weapons\",\"armour\":\"Armour\",\"ammo\":\"Ammunition\",\"magic\":\"Spells and gems\",\"consumables\":\"Consumables\",\"materials\":\"Materials\",\"tools\":\"Tools\",\"building\":\"Building\",\"cosmetics\":\"Cosmetics and collectibles\",\"dyes\":\"Dyes\"}},\"knowledge\":{\"noBlock\":\"This save has no knowledge block.\",\"search\":\"Search by name or id\",\"above\":\"Above level\",\"allLevels\":\"all\",\"show\":\"Show\",\"filter\":{\"itemsRecipes\":\"Items and recipes\",\"items\":\"Items only\",\"recipes\":\"Recipes only\",\"everything\":\"Everything\"},\"removeN\":\"Remove {n}\",\"removeTitle\":\"Remove {n} knowledge entries?\",\"removeBody\":\"Recipes that depend on these entries may also close. Changes are written only when you save.\",\"kind\":\"Kind\",\"name\":\"Name\",\"state\":\"State\",\"id\":\"id {id}\",\"kinds\":{\"item\":\"item\",\"recipe\":\"recipe\",\"misc\":\"other\"},\"count\":{\"one\":\"{n} of {total} entry\",\"other\":\"{n} of {total} entries\"},\"unnamed\":{\"one\":\"{count} with no known name\",\"other\":\"{count} with no known name\"},\"shownLimit\":\"Showing the first 400; use the search or the level cut-off to see the rest.\",\"hint\":\"Removing a high-level material closes the recipes that depend on it. Recipes are not stored in the save: the game recomputes them from the knowledge, both here and in the server world.\"},\"skills\":{\"total\":\"Total points\",\"minusLevel\":\"−1 level\",\"plusLevel\":\"+1 level\",\"tree\":\"Skill tree\",\"used\":\"In use\",\"available\":\"Available\",\"enterTotal\":\"enter total…\",\"changeTotal\":\"Click to change the total\",\"totalFromGame\":\"Total {total}: 2 per level above 1 + cleared Shroud Roots and Elixir Wells. Calculated, not stored in the save.\",\"totalHint\":\"Point total (in use + available, as the game shows it):\",\"zoomIn\":\"Zoom in\",\"zoomOut\":\"Zoom out\",\"centre\":\"Centre\",\"cost\":\"cost {n}\",\"maxN\":\"max {n}\",\"deckHint\":\"A: +1 level · Y: −1\",\"hint\":\"Click: +1 level · Right click: −1\",\"nodeLabel\":\"{name}: level {level} of {max}\",\"keyboardHint\":\"Click / Enter / Space: +1 level · Right click / −: −1 · Arrows: browse\",\"class\":{\"warrior\":\"Warrior\",\"ranger\":\"Ranger\",\"mage\":\"Mage\",\"neutral\":\"Neutral\"}},\"deck\":{\"delete\":\"Delete\",\"range\":\"From {min} to {max}\",\"min\":\"Min\",\"use\":\"Use\",\"current\":\"Now\"},\"art\":{\"preparing\":\"Preparing game art…\",\"needsClient\":\"Game art needs the Enshrouded client (Settings → Games).\",\"notFound\":\"Game art not found. Run {script}.\"},\"game\":{\"tree\":{\"aboveMax\":\"{no}: level {nivel} above the maximum of {max}\",\"orphans\":\"Nodes with no link to the rest of the tree: {nos}\",\"overTotal\":\"Points in use ({usados}) exceed the character total ({total})\"},\"unknownItem\":\"Unknown item (0x{h})\",\"errors\":{\"unknownItems\":\"Unknown items in this save (a mod that is gone?): read-only\",\"levelXp\":\"Invalid level or XP\",\"skill\":\"Invalid skill\",\"item\":\"Unknown item: {id}\",\"slotAmount\":\"Invalid slot or amount\",\"sameSlot\":\"Two items in the same inventory slot\",\"knowledge\":\"Invalid knowledge entry\",\"knowledgeRepeated\":\"Knowledge with a repeated id\",\"closeGame\":\"Close Enshrouded before saving: the game would overwrite the edit.\",\"notOpen\":\"Save not open for editing\",\"noEquipment\":\"The editor cannot find the equipment and inventory in this save\",\"skillList\":\"The editor does not know this save's skill list\",\"notSave\":\"Not an Enshrouded save\",\"inventoryUnsure\":\"The editor could not find this save's inventory for sure: no items can be written into it.\",\"modData\":\"This save holds data of a mod the editor cannot write ({mods}): read-only, so the mod's data stays as it is.\",\"inventoryUnread\":\"The editor could not read this character's inventory (backpack, swap bar and action bar) for sure: read-only, so nothing is written over it.\"},\"saveFileFilter\":\"Enshrouded save\",\"rarity\":{\"none\":\"No rarity\",\"common\":\"Common\",\"uncommon\":\"Uncommon\",\"rare\":\"Rare\",\"epic\":\"Epic\",\"legendary\":\"Legendary\"},\"category\":{\"weapon\":\"Weapon\",\"equipment\":\"Equipment\",\"material\":\"Material\",\"consumable\":\"Consumable\",\"ammunition\":\"Ammunition\",\"tool\":\"Tool\",\"building\":\"Building\",\"blueprint\":\"Blueprint\",\"collectible\":\"Collectible\",\"cosmetic\":\"Cosmetic\",\"gem\":\"Gem\"},\"attribute\":{\"constitution\":\"Constitution\",\"spirit\":\"Spirit\",\"endurance\":\"Endurance\",\"strength\":\"Strength\",\"dexterity\":\"Dexterity\",\"intelligence\":\"Intelligence\"},\"classes\":{\"survivor\":\"Survivor\",\"beastmaster\":\"Beastmaster\",\"ranger\":\"Ranger\",\"assassin\":\"Assassin\",\"trickster\":\"Trickster\",\"wizard\":\"Wizard\",\"healer\":\"Healer\",\"battlemage\":\"Battlemage\",\"tank\":\"Tank\",\"warrior\":\"Warrior\",\"barbarian\":\"Barbarian\",\"athlete\":\"Athlete\"},\"knowledge\":{\"pickedUp\":\"picked up\",\"used\":\"used\",\"inspected\":\"inspected\",\"seen\":\"seen\",\"crafted\":\"crafted\"}}}"),
	sfBackend: {
		backend: {
			title: "ShroudForge",
			sub: "Which ShroudForge runs this game's ShroudForge mods. Saved in kiln/kiln.json; it applies from the next start.",
			label: "ShroudForge backend",
			choice: {
				hosted: "Kiln's bundled ShroudForge",
				installed: "My installed ShroudForge",
				kiln: "Kiln's interpreter"
			},
			about: {
				hosted: "The ShroudForge that comes with Kiln.",
				installed: "The ShroudForge in the game's shroudforge folder.",
				kiln: "Kiln runs ShroudForge mods itself. A mod it cannot run whole is skipped, with the reason, before Play."
			},
			none: "no ShroudForge",
			default: "Default",
			held: "Held",
			heldWhy: "Not available in this Kiln build: {reason}",
			set: {
				default: "Nothing set: the default.",
				explicit: "Chosen: {name}.",
				inherited: "From the older switch \"Use my installed ShroudForge\": {name}. A choice here replaces it.",
				invalid: "kiln.json names a backend Kiln does not know ({value}): no ShroudForge runs this game's ShroudForge mods until you choose one."
			},
			next: "Next start: {name}",
			last: "Last start: {name}",
			envRuntime: "KILN_SHROUDFORGE_RUNTIME is set where the launcher runs: a start from the launcher follows it.",
			envOff: "KILN_SHROUDFORGE=0 is set where the launcher runs: no ShroudForge runs at a start from the launcher.",
			route: {
				missing: "Kiln's interpreter is not in this game yet: install Kiln again to add it.",
				ownLoader: "ShroudForge's own loader is in the game ({file}): it runs the ShroudForge mods, and Kiln's choices stand aside."
			},
			running: "Close the game to change this.",
			reset: "Back to the default",
			unreadable: "This cannot be changed while kiln.json cannot be read: {reason}",
			saved: "Saved. Next start: {name}.",
			ask: {
				title: "Change the ShroudForge backend?",
				to: "From the next start: {name}",
				replaces: "This replaces the older switch \"Use my installed ShroudForge\".",
				kiln: "Mods Kiln's interpreter cannot run whole are skipped at the start, each with its reason, and the warning before Play lists them. Nothing switches back to Kiln's bundled ShroudForge on its own.",
				unchanged: "kiln.json already says this.",
				refused: "This cannot be chosen now: {reason}",
				when: "Nothing changes until you save. The game reads it at its next start."
			}
		},
		startWarning: {
			title: "Some mods will not run this start",
			continue: "Continue without these mods",
			game: "Game: {path}",
			changedTitle: "Changed",
			changed: "What will not run this start changed since the last warning. Read it again before you continue.",
			otherGame: "This warning belongs to another game. Press Play again.",
			lead: {
				save: {
					title: "Do not save without them",
					text: "A mod below keeps data in your saves. If you save a world while it is off, its data may change or be lost. Play without saving, or cancel and fix it first."
				},
				check: {
					title: "Not everything could be checked",
					text: "Kiln could not check everything for this start, so these mods may not run. Read the reasons below."
				},
				plain: {
					title: "These mods will not run",
					text: "The game starts without the mods below. The rest of your mods run as usual."
				}
			},
			listAria: "What will not run ({count})",
			notesAria: "Good to know",
			nothingChanged: "Continuing switches nothing off and saves or deletes nothing. The next Play checks again.",
			partData: "startup data",
			partLua: "Lua",
			partDlls: "DLLs: {list}",
			parts: "Parts: {list}",
			dataNotApplied: "Its startup data is not applied this start.",
			chain: "Because of: {chain}",
			folder: "Folder: {root}",
			wasActive: "It was active at an earlier start ({launch}).",
			unknownStart: "start unknown",
			saveLine: "It keeps data in your saves: do not save a world while it is off.",
			saveUnknown: "It may keep data in your saves: do not save a world while it is off.",
			skipped: "{name} does not run",
			refused: "No ShroudForge mod runs this start",
			notStarted: "Not started: {list}",
			previous: "{name} is off now",
			offNow: "It does not run this start.",
			basisPolicy: "Kiln's list of mods that keep save data names it.",
			basisUnknown: "It has both game data and code, so it may keep save data.",
			about: "About: {list}",
			inspector: "Kiln's archive inspector cannot be used",
			planWarning: "Launch plan warning",
			baseTitle: "Game files this start is based on",
			baseRestore: "To go back to exactly these files, switch off the mods that change the game's data and start the game through Kiln once (Logs, Engines: Restore the game's data).",
			baseVerify: "For the game's files as Steam ships them, use Verify integrity of game files in Steam.",
			costTitle: "Slower start",
			noteTitle: "Note",
			keybindsTitle: "Keyboard shortcuts",
			keybinds: "{name}: its keyboard shortcuts do not work with Kiln's interpreter yet. The rest of the mod runs."
		},
		engineRow: {
			plugin: "Kiln's interpreter (kiln-sfi)",
			packages: "ShroudForge mods",
			phase: {
				starting: "starting",
				running: "running",
				ended: "ended",
				aside: "stood aside",
				refused: "not started",
				unknown: "unknown"
			},
			package: {
				running: "running",
				dataApplied: "data applied",
				off: "off",
				skipped: "skipped"
			},
			stale: {
				"older-start": "This record is from an earlier start: what the interpreter did in the newest start is not known.",
				"not-running": "The record says {phase}, but the game is not running: that start ended without its last record (it may have crashed).",
				"other-process": "The record is from another game process: it is not this start's."
			},
			previous: {
				incomplete: "The start before this one ended without finishing its record (it may have crashed).",
				suspect: "Kiln could not read what the start before this one left."
			},
			unreadable: "Kiln's interpreter left a record Kiln cannot read: {error}"
		},
		restoreBase: {
			title: "Restore the game's data",
			base: "Kiln started from these game files: {label}.",
			how: "To go back to exactly these files, switch off the mods that change the game's data and start the game through Kiln once. Kiln puts back only data it can prove it wrote; another loader's data stays.",
			save: "Mods that keep data in your saves: do not save a world while they are off.",
			vanilla: "These are not necessarily the files as Steam ships them. For those, use Verify integrity of game files in Steam (the game's Properties, Installed Files).",
			unknown: "Kiln cannot tell which game files it would put back: {reason}."
		}
	},
	registry: {
		title: "Registry conflicts",
		gameUnavailable: "Game selection unavailable",
		noGame: "No game selected",
		noGameHint: "Choose an installed game in Settings to read its registry snapshot.",
		sub: "The saved registry snapshot for this game, not live telemetry or a prediction of the next start.",
		refresh: "Refresh report",
		unavailable: "Registry report unavailable",
		missing: "No registry report yet",
		missingHint: "Kiln has not written kiln/state/registry.json for this game. There is no registry snapshot to inspect.",
		stale: "Stale snapshot",
		staleBody: "{reason} Values below belong to the saved snapshot, not necessarily the current world or mod list.",
		snapshot: "Snapshot",
		snapshotSub: "{time} · generation {generation} · {phase}",
		baseOrigin: "Base origin: {origin}",
		archiveWriter: "Archive writer: {writer}",
		unverified: "Mapping unverified",
		unverifiedBody: "Field edits may be refused as Unproven. Read each refusal for its reason.",
		certaintyHint: "Certain = Kiln observed the writer. Named from journal = archive-writer attribution. Best-effort drift is not proof of who wrote it or that no other writer exists.",
		partial: "Partial report",
		partialRows: {
			one: "{count} row was omitted by the native report or this view's safety limits.",
			other: "{count} rows were omitted by the native report or this view's safety limits."
		},
		partialWriters: {
			one: "{count} earlier writer was left out of the writer chains.",
			other: "{count} earlier writers were left out of the writer chains."
		},
		partialBody: "Text and list values are also bounded; this is not the full history.",
		empty: "No registry edits in this snapshot",
		emptyHint: "The report is present and valid, but contains no fields, conflicts, refusals or entry changes.",
		refused: "Refused edits",
		refusedSub: "These edits were not applied. RestartRequired needs a game restart; Unproven needs a proven mapping in Kiln.",
		noRefused: "No refused edits in the displayed report.",
		conflicts: "Conflicts and drift",
		conflictsSub: "Winners and other owners are as recorded at snapshot time.",
		noConflicts: "No conflict lines in the displayed report.",
		conflictLine: "{kind} · Winner: {winner} · Other owner: {loser}",
		fields: "Fields and writer chains",
		fieldsSub: "Earlier writers first; expand a field for its base, operations and result.",
		filter: "Filter fields or owners",
		noMatch: "No displayed fields match this filter.",
		noFields: "No field rows in the displayed report.",
		entries: "Entry changes",
		noEntries: "No entry changes.",
		added: "Added",
		removed: "Removed",
		previous: "Previous",
		next: "Next",
		page: "Page {page} of {pages} · {count} displayed rows",
		summary: "Winner: {winner} · Result: {result}",
		summaryOmitted: {
			one: "{count} earlier writer omitted",
			other: "{count} earlier writers omitted"
		},
		applies: "Applies: {applies} · Base origin: {origin}",
		baseValue: "Base value: {value}",
		writersOmitted: {
			one: "{count} earlier writer omitted. Only the retained end of the chain is shown.",
			other: "{count} earlier writers omitted. Only the retained end of the chain is shown."
		},
		element: "Element: {element}",
		operation: "Operation: {operation}",
		after: "After: {value}",
		noWriters: "No writer rows; the base decides the result.",
		formula: "Formula inputs (evaluation order): {inputs}",
		winner: "Winner: {winner}",
		result: "Result:",
		certainty: {
			certain: "Certain",
			named: "Named from journal",
			bestEffort: "Best-effort drift",
			unknown: "Unknown"
		},
		advancedField: "Advanced field (details hidden)",
		advancedReason: "Advanced field (mapping details hidden).",
		advancedConflict: "Advanced field conflict; mapping details hidden.",
		unknownField: "{field} (Field unknown to this launcher)",
		elementHidden: "element details hidden",
		baseOwner: "Base",
		noOwner: "None",
		referenceCheck: "Kiln reference check",
		referenceOp: "Reference check",
		fresh: {
			clock: "The report clock cannot be verified.",
			process: "The current game process cannot be verified.",
			stopped: "The game is not running; this is its last saved snapshot.",
			previousStart: "The report predates the current game start.",
			old: "This snapshot is more than 24 hours old.",
			start: "The current game start cannot be verified."
		},
		error: {
			tooLarge: "Registry report exceeds the 8 MiB limit this view reads. Open kiln/state/registry.json in the game folder to read it in full.",
			nesting: "Registry report nesting limit exceeded.",
			structure: "Registry report structure limit exceeded.",
			notJson: "Registry report is not valid JSON.",
			format: "Unsupported registry report format (expected 1).",
			malformed: "Invalid registry report ({part})."
		}
	},
	playerStorage: {
		tab: "Storage",
		title: "Player storage",
		readOnly: "Read-only. These items wait in Kiln, separate from your backpack.",
		empty: "No player storage for this character.",
		return: "Return to character",
		returnTitle: "Return storage to this character?",
		returnBody: "Kiln makes a backup first, then adds as many base-game stacks as fit in extra backpack pages. Mod items and anything else stay in storage. The game and Steam must be closed.",
		serverCopy: "A Kiln server keeps its own copy of this storage. If you join it without Kiln, that server can give its copy back there.",
		confirm: "Return items",
		returning: "Returning…",
		result: "Returned {returned} stacks; {remaining} stacks remain in storage.",
		closeGame: "Close Enshrouded and Steam before returning storage.",
		localOnly: "Return is only available for LOCAL characters, not Steam Cloud saves.",
		saveFirst: "Save or discard your edits before returning storage.",
		unavailable: "Storage cannot be returned",
		restoreWarning: "This character has player storage. An older backup may pair with older storage; if its pair is no longer kept, items moved since that backup could be lost or duplicated.",
		unpaired: "This character changed since Kiln last saw its storage, so nothing can be returned to it. The newest storage copy is shown.",
		parked: "Mod off",
		kept: "kiln/data/characters and kiln/data/worlds stay. They keep player storage safe until you install Kiln again.",
		unreadable: "Some stored records cannot be shown. They are left unchanged.",
		modItems: "{count} stacks of mod items stay in storage: they need their mod."
	}
};
function ie(e) {
	let t = (e ?? "").trim().replace(/_/g, "-").toLowerCase();
	return t.length === 0 ? "en" : t;
}
function ae(e) {
	let t = ie(e), n = [], r = (e) => {
		n.includes(e) || n.push(e);
	};
	r(t), (/^zh-hans(-|$)/.test(t) || t === "zh-sg") && r("zh-cn"), (/^zh-hant(-|$)/.test(t) || t === "zh-hk" || t === "zh-mo") && r("zh-tw");
	let i = t.split("-")[0];
	return t === "es-mx" && r("es-419"), r(i), i === "nb" ? r("no") : i === "no" && r("nb"), r("en"), n;
}
function oe(e, t = "", n = {}, r = []) {
	if (typeof e != "object" || !e || Array.isArray(e)) return n;
	for (let [i, a] of Object.entries(e)) {
		let e = t ? `${t}.${i}` : i;
		typeof a == "string" ? n[e] = a : typeof a == "object" && a && !Array.isArray(a) ? oe(a, e, n, r) : r.push(e);
	}
	return n;
}
function se(e, t) {
	let n = "";
	for (let r = 0; r < e.length; r++) {
		let i = e[r];
		if (i === "{" && e[r + 1] === "{") n += "{", r++;
		else if (i === "}" && e[r + 1] === "}") n += "}", r++;
		else if (i === "{") {
			let a = e.indexOf("}", r + 1);
			if (a < 0) {
				n += i;
				continue;
			}
			let o = t(e.slice(r + 1, a));
			n += o ?? e.slice(r, a + 1), r = a;
		} else n += i;
	}
	return n;
}
function ce(e) {
	if (e.length !== 1) return !1;
	let t = e[0];
	return typeof t == "object" && !!t && Object.getPrototypeOf(t) === Object.prototype;
}
function le(e, t) {
	if (/^\d+$/.test(e)) {
		let n = Number(e);
		return ce(t) || n >= t.length ? void 0 : { v: t[n] };
	}
	if (ce(t) && Object.prototype.hasOwnProperty.call(t[0], e)) return { v: t[0][e] };
}
var ue = /* @__PURE__ */ new Map();
function de(e, t) {
	let n = `${e}|${JSON.stringify(t)}`, r = ue.get(n);
	if (!r) {
		try {
			r = new Intl.NumberFormat(e, t);
		} catch {
			r = new Intl.NumberFormat("en", t);
		}
		ue.set(n, r);
	}
	return r;
}
function fe(e, t, n) {
	let r = /^([NnFfDdPp])(\d{0,2})$/.exec(t.trim());
	if (!r || !Number.isFinite(e)) return null;
	let i = r[2] === "" ? null : Number(r[2]);
	switch (r[1].toUpperCase()) {
		case "N": return de(n, {
			minimumFractionDigits: i ?? 2,
			maximumFractionDigits: i ?? 2
		}).format(e);
		case "F": return de(n, {
			minimumFractionDigits: i ?? 2,
			maximumFractionDigits: i ?? 2,
			useGrouping: !1
		}).format(e);
		case "D": return Number.isInteger(e) ? (e < 0 ? "-" : "") + String(Math.abs(e)).padStart(i ?? 1, "0") : null;
		case "P": return de(n, {
			style: "percent",
			minimumFractionDigits: i ?? 2,
			maximumFractionDigits: i ?? 2
		}).format(e);
	}
	return null;
}
function pe(e, t, n) {
	return e == null ? "" : t !== null && typeof e == "number" ? fe(e, t, n) ?? String(e) : String(e);
}
function me(e, t, n = "en") {
	return e.indexOf("{") < 0 && e.indexOf("}") < 0 ? e : se(e, (e) => {
		let r = e.indexOf(":"), i = (r >= 0 ? e.slice(0, r) : e).trim(), a = r >= 0 ? e.slice(r + 1) : null;
		if (!i) return;
		let o = le(i, t);
		return o ? pe(o.v, a, n) : void 0;
	});
}
function he(e) {
	if (ce(e)) {
		let t = e[0].count;
		return typeof t == "number" ? t : null;
	}
	return typeof e[0] == "number" ? e[0] : null;
}
var ge = /* @__PURE__ */ new Map();
function _e(e, t) {
	let n = ge.get(t);
	if (!n) {
		try {
			n = new Intl.PluralRules(t);
		} catch {
			n = new Intl.PluralRules("en");
		}
		ge.set(t, n);
	}
	return n.select(e);
}
function ve(e, t, n, r) {
	if (!e) return;
	let i = e[t];
	return i === void 0 ? n === null ? e[`${t}.other`] : e[`${t}.${_e(n, r)}`] ?? e[`${t}.other`] : i;
}
// src/i18n/index.ts
var ye = /* @__PURE__ */ new Map([["en", {
	tag: "en",
	table: oe(re)
}]]), be = /* @__PURE__ */ new Map(), xe = new Map(ye), Se = "system", Ce = "en", we = ["en"], Te = 0, Ee = /* @__PURE__ */ new Set(), De = /* @__PURE__ */ new Set(), Oe = (() => {
	try {
		return !1;
	} catch {
		return !1;
	}
})();
function ke() {
	xe.clear();
	for (let [e, t] of ye) xe.set(e, t);
	for (let [e, t] of be) e !== "en" && xe.set(e, t);
	De.clear(), Ie(!0);
}
function Ae(e, t) {
	ie(e) !== "en" && (ye.set(ie(e), {
		tag: e,
		table: oe(t)
	}), ke());
}
function je() {
	return [...xe.values()].sort((e, t) => e.tag === "en" ? -1 : t.tag === "en" ? 1 : e.tag.localeCompare(t.tag)).map((e) => ({
		tag: e.tag,
		name: Ne(e.tag)
	}));
}
var Me = {
	"zh-cn": "简体中文",
	"zh-tw": "繁體中文",
	"zh-hans": "简体中文",
	"zh-hant": "繁體中文"
};
function Ne(e) {
	let t = xe.get(ie(e))?.table["language.name"];
	if (t) return t;
	if (Me[ie(e)]) return Me[ie(e)];
	try {
		return new Intl.DisplayNames([e], { type: "language" }).of(e) ?? e;
	} catch {
		return e;
	}
}
function Pe() {
	let e = typeof navigator > "u" ? void 0 : navigator;
	return [...e?.languages?.length ? e.languages : e?.language ? [e.language] : []];
}
function Fe(e, t = Pe()) {
	if (e !== "system") return ie(e);
	for (let e of t) if (ae(e).find((e) => e !== "en" && xe.has(e))) return ie(e);
	return "en";
}
function Ie(e = !1) {
	let t = Fe(Se), n = ae(t).filter((e) => xe.has(e));
	if (e || t !== Ce || n.join() !== we.join()) {
		Ce = t, we = n, Te++, typeof document < "u" && (document.documentElement.lang = xe.get(we[0])?.tag ?? t);
		for (let e of Ee) e();
	}
}
function Le(e) {
	Se = !e || e === "system" ? "system" : e, Ie();
}
var Re = () => we[0] ?? "en", ze = () => xe.get(Re())?.tag ?? "en", Be = () => Re() === "en" ? void 0 : ze();
function Ve(e, t) {
	Oe && !De.has(`${t}|${e}`) && (De.add(`${t}|${e}`), t === "en" ? console.error(`[i18n] no text for "${e}" in en.json`) : console.warn(`[i18n] "${e}" is not in ${t}: shown in English`));
}
function A(e, ...t) {
	let n = he(t);
	for (let r of we) {
		let i = xe.get(r), a = ve(i.table, e, n, i.tag);
		if (a !== void 0) return r === "en" && we[0] !== "en" && Ve(e, xe.get(we[0]).tag), me(a, t, ze());
	}
	return Ve(e, "en"), e;
}
function j(e, ...t) {
	let n = [], r = (e) => p(e) ? (n.push(e), `\u0000${n.length - 1}\u0000`) : e, i = A(e, ...t.length === 1 && t[0] !== null && typeof t[0] == "object" && Object.getPrototypeOf(t[0]) === Object.prototype ? [Object.fromEntries(Object.entries(t[0]).map(([e, t]) => [e, r(t)]))] : t.map(r));
	if (n.length === 0) return i;
	let a = i.split(/\u0000(\d+)\u0000/);
	return d(l, null, ...a.map((e, t) => t % 2 == 1 ? d(l, { key: t }, n[Number(e)]) : e));
}
function He(e) {
	return Ee.add(e), () => {
		Ee.delete(e);
	};
}
var Ue = () => Te;
function M() {
	let e = S(He, Ue, Ue);
	return y(() => (e, ...t) => A(e, ...t), [e]);
}
// src/api/shared.ts
function We(e) {
	return typeof e == "object" && !!e && "code" in e && "message" in e;
}
function Ge(e) {
	return We(e) ? e : {
		code: "internal",
		message: e instanceof Error ? e.message : String(e)
	};
}
// src/api/admin.ts
var Ke = "kap/1", qe = () => void 0, N = null, Je = { useArtStatus: qe };
function Ye(e) {
	if (e.transport !== N) {
		for (let e of ot.values()) e.retry && clearTimeout(e.retry), e.release && clearTimeout(e.release);
		ot.clear(), et.setState({ conns: {} });
		let t = dt;
		dt = null, t?.then((e) => e()).catch(() => void 0), N = e.transport;
	}
	Je = {
		...e.host,
		useArtStatus: e.host?.useArtStatus ?? qe
	};
}
function Xe() {
	if (!N) throw {
		code: "admin_unavailable",
		message: A("api.admin.noTransport")
	};
	return N;
}
var Ze = () => N, Qe = () => Je, $e = (e) => ({
	serverId: e,
	status: "idle",
	session: null,
	want: ct(e),
	error: null,
	state: null,
	stateSeq: 0,
	stateAt: null,
	connectedAt: null,
	attempt: 0,
	retryAt: null,
	gaps: 0
}), et = C(() => ({ conns: {} })), tt = (e) => et.getState().conns[e] ?? $e(e), nt = (e, t) => et.setState((n) => ({ conns: {
	...n.conns,
	[e]: {
		...n.conns[e] ?? $e(e),
		...t
	}
} })), rt = [
	1,
	2,
	4,
	8,
	15,
	30
], it = 3e4, at = 6e3, ot = /* @__PURE__ */ new Map(), st = (e) => {
	let t = ot.get(e);
	return t || ot.set(e, t = {
		refs: 0,
		gen: 0,
		retry: null,
		release: null
	}), t;
};
function ct(e) {
	try {
		let t = localStorage.getItem(`kiln.admin.transport.${e}`);
		return t === "tcp" || t === "file" || t === "ssh" ? t : "auto";
	} catch {
		return "auto";
	}
}
var lt = [
	"players",
	"deaths",
	"combatDeaths"
];
function ut(e) {
	let t = e;
	for (let n of lt) {
		let r = e[n];
		r === void 0 || Array.isArray(r) || (t === e && (t = { ...e }), t[n] = r && typeof r == "object" ? Object.values(r) : []);
	}
	return t;
}
var dt = null;
function ft() {
	return dt ??= Xe().listen(({ session: e, serverId: t, event: n }) => {
		let r = et.getState().conns[t];
		if (r?.session && r.session.session === e) {
			if (n.kind === "push") {
				if (n.topic === "state" && n.data && typeof n.data == "object" && !Array.isArray(n.data)) {
					let e = ut(n.data), i = +(r.stateSeq > 0 && n.seq > r.stateSeq + 1);
					nt(t, {
						state: e,
						stateSeq: n.seq,
						stateAt: Date.now(),
						gaps: r.gaps + i
					});
					for (let n of gt) try {
						n(t, e);
					} catch (e) {
						console.error("admin state listener", e);
					}
				} else for (let e of _t) e(t, n.topic, n.data);
			} else if (n.kind === "closed") {
				if (bt.delete(e)) return;
				mt(t, n.reason, n.retry);
			} else n.kind === "error" ? nt(t, { error: {
				code: "internal",
				message: n.message
			} }) : n.kind === "ready" && ht(t, n);
		}
	}), dt;
}
var pt = 1e4;
function mt(e, t, n) {
	let r = st(e);
	if (tt(e).session) {
		if (r.retry && clearTimeout(r.retry), r.retry = null, n === !1) r.gen++, nt(e, {
			status: "error",
			session: null,
			retryAt: null,
			error: {
				code: t === "auth_failed" || t === "token" ? "auth_failed" : "admin_unavailable",
				message: A("api.admin.sessionClosed", { reason: t })
			}
		});
		else if (nt(e, {
			status: "reconnecting",
			error: {
				code: "admin_unavailable",
				message: A("api.admin.sessionClosedReconnecting", { reason: t })
			}
		}), n === void 0) {
			let n = r.gen;
			r.retry = setTimeout(() => {
				r.retry = null, n === r.gen && tt(e).status === "reconnecting" && Ct(e, {
					code: "admin_unavailable",
					message: A("api.admin.sessionClosed", { reason: t })
				});
			}, pt);
		}
	}
}
function ht(e, t) {
	let n = st(e), r = tt(e);
	r.session && (n.retry && clearTimeout(n.retry), n.retry = null, nt(e, {
		status: "ready",
		session: {
			...r.session,
			transport: t.transport ?? r.session.transport,
			ops: t.ops ?? r.session.ops,
			topics: t.topics ?? r.session.topics,
			...t.restart ? { restart: t.restart } : {}
		},
		error: null,
		attempt: 0,
		connectedAt: Date.now(),
		stateSeq: 0
	}));
}
var gt = /* @__PURE__ */ new Set(), _t = /* @__PURE__ */ new Set();
function vt(e) {
	return gt.add(e), () => gt.delete(e);
}
function yt(e) {
	return _t.add(e), () => _t.delete(e);
}
var bt = /* @__PURE__ */ new Set();
function xt(e) {
	bt.add(e), setTimeout(() => bt.delete(e), 6e4);
	try {
		return Xe().disconnect(e).catch(() => void 0);
	} catch {
		return Promise.resolve();
	}
}
async function St(e) {
	let t = st(e);
	t.retry && clearTimeout(t.retry), t.retry = null;
	let n = ++t.gen, r = tt(e);
	nt(e, {
		status: r.status === "ready" || r.attempt > 0 || r.session ? "reconnecting" : "connecting",
		retryAt: null
	}), r.session && await xt(r.session.session);
	try {
		await ft();
		let r = await Xe().connect(e, tt(e).want);
		if (n !== t.gen) {
			xt(r.session);
			return;
		}
		nt(e, {
			status: "ready",
			session: r,
			error: null,
			attempt: 0,
			connectedAt: Date.now(),
			stateSeq: 0,
			gaps: 0
		}), r.topics.includes("state") && await Xe().subscribe(r.session, "state").catch((t) => nt(e, { error: Ge(t) }));
	} catch (r) {
		if (n !== t.gen) return;
		Ct(e, Ge(r));
	}
}
function Ct(e, t) {
	let n = st(e), r = tt(e), i = r.attempt + 1, a = rt[Math.min(rt.length - 1, i - 1)] * 1e3;
	n.gen++, r.session && xt(r.session.session);
	let o = n.refs > 0 && t.code !== "auth_failed";
	nt(e, {
		status: "error",
		session: null,
		error: t,
		attempt: i,
		retryAt: o ? Date.now() + a : null
	}), n.retry && clearTimeout(n.retry), n.retry = o ? setTimeout(() => St(e), a) : null;
}
function wt(e) {
	let t = st(e);
	t.refs++, t.release && clearTimeout(t.release), t.release = null;
	let n = tt(e);
	(n.status === "idle" || n.status === "error" && !t.retry && n.error?.code !== "auth_failed") && St(e);
}
function Tt(e) {
	let t = st(e);
	t.refs = Math.max(0, t.refs - 1), t.refs > 0 || (t.release && clearTimeout(t.release), t.release = setTimeout(() => {
		if (t.refs > 0) return;
		t.gen++, t.retry && clearTimeout(t.retry), t.retry = null;
		let n = tt(e);
		n.session && xt(n.session.session), nt(e, {
			...$e(e),
			want: n.want
		});
	}, it));
}
function Et(e, t) {
	if (t) {
		nt(e, { want: t });
		try {
			localStorage.setItem(`kiln.admin.transport.${e}`, t);
		} catch {}
	}
	nt(e, { attempt: 0 }), St(e);
}
var Dt = (e, t) => new Promise((n, r) => {
	let i = tt(e);
	if (i.status === "ready") return n(i);
	let a = setTimeout(() => {
		o(), r(tt(e).error ?? {
			code: "admin_unavailable",
			message: A("api.admin.notConnected")
		});
	}, t), o = et.subscribe((t) => {
		let r = t.conns[e];
		r?.status === "ready" && (clearTimeout(a), o(), n(r));
	});
}), Ot = (e) => ({
	code: "unsupported",
	message: A("api.admin.needsNewer", { op: e })
});
async function kt(e, t, n = {}) {
	let r = (tt(e).status === "ready" ? tt(e) : await Dt(e, 8e3)).session;
	if (!r.ops.includes(t)) throw Ot(t);
	try {
		return await Xe().call(r.session, t, n);
	} catch (t) {
		let n = Ge(t);
		throw tt(e).session?.session === r.session && (n.code === "not_found" ? Ct(e, n) : n.code === "admin_unavailable" && tt(e).status === "ready" && nt(e, {
			status: "reconnecting",
			error: n
		})), n;
	}
}
var At = u(null);
function jt({ serverId: e, children: t }) {
	return g(() => (wt(e), () => Tt(e)), [e]), d(At.Provider, { value: e }, t);
}
function Mt(e) {
	let t = h(At), n = e ?? t;
	if (!n) throw Error("admin hooks need a serverId or an <AdminProvider>");
	return n;
}
var Nt = () => h(At) !== null;
function Pt(e, t) {
	let n = h(At) === e;
	g(() => {
		if (t && !n) return wt(e), () => Tt(e);
	}, [
		e,
		t,
		n
	]);
}
function Ft(e) {
	let t = Mt(e);
	Pt(t, e !== void 0);
	let n = et((e) => e.conns[t]);
	return y(() => {
		let e = n ?? $e(t);
		return {
			...e,
			transport: e.session?.transport ?? null,
			connecting: e.status === "connecting" || e.status === "reconnecting",
			ready: e.status === "ready",
			ops: new Set(e.session?.ops ?? [])
		};
	}, [n, t]);
}
function P(e, t) {
	let n = t !== void 0, r = Mt(n ? e : void 0), i = n ? t : e;
	return et((e) => !!e.conns[r]?.session?.ops.includes(i));
}
function It(e) {
	let t = Mt(e);
	return Pt(t, e !== void 0), y(() => (e, n) => kt(t, e, n ?? {}), [t]);
}
function F(e, t = {}) {
	let { invalidates: n, serverId: r, onSuccess: i, ...a } = t, s = Mt(r), l = c();
	return o({
		mutationKey: [
			"admin",
			s,
			e
		],
		mutationFn: (t) => kt(s, e, t ?? {}),
		onSuccess: (...e) => (n?.length && Lt(l, s, n), i?.(...e)),
		...a
	});
}
function I(e, t = {}, n = {}) {
	let { serverId: r, enabled: i, ...a } = n, o = Mt(r), c = et((e) => e.conns[o]?.status === "ready" ? e.conns[o]?.session : null), l = !!c?.ops.includes(e);
	return s({
		queryKey: [
			"admin",
			o,
			e,
			t,
			c?.session ?? null
		],
		queryFn: () => kt(o, e, t),
		enabled: l && (i ?? !0),
		retry: !1,
		...a
	});
}
function Lt(e, t, n) {
	let r = new Set(n);
	return e.invalidateQueries({ predicate: (e) => e.queryKey[0] === "admin" && e.queryKey[1] === t && r.has(String(e.queryKey[2])) });
}
function Rt(e) {
	return {
		adminInfo: () => e("admin_info"),
		serverRestart: (t = {}) => e("server_restart", { ...t }),
		serverRestartCancel: (t = {}) => e("server_restart_cancel", { ...t }),
		loginMint: (t) => e("login_mint", { ...t }),
		loginRedeem: (t) => e("login_redeem", { ...t }),
		updateList: () => e("update_list"),
		updateStage: (t) => e("update_stage", { ...t }),
		updateCancel: (t) => e("update_cancel", { ...t }),
		updateRollback: (t) => e("update_rollback", { ...t })
	};
}
function zt(e) {
	let t = It(e);
	return y(() => Rt(t), [t]);
}
// src/admin/history.ts
var Bt = () => ({
	players: {},
	deaths: [],
	graves: [],
	waypoints: [],
	enemyLabels: {}
}), Vt = (e) => `kiln.admin.history.v1.${e}`;
function Ht(e) {
	try {
		let t = localStorage.getItem(Vt(e));
		return t ? {
			...Bt(),
			...JSON.parse(t)
		} : Bt();
	} catch {
		return Bt();
	}
}
var Ut = /* @__PURE__ */ new Map();
function Wt(e, t) {
	clearTimeout(Ut.get(e)), Ut.set(e, setTimeout(() => {
		try {
			localStorage.setItem(Vt(e), JSON.stringify(t));
		} catch {}
	}, 400));
}
var Gt = C(() => ({ by: {} }));
function Kt(e) {
	let t = Gt.getState().by[e];
	if (t) return t;
	let n = Ht(e);
	return Gt.setState((t) => ({ by: {
		...t.by,
		[e]: n
	} })), n;
}
function qt(e, t) {
	let n = Kt(e), r = t(n);
	r && r !== n && (Gt.setState((t) => ({ by: {
		...t.by,
		[e]: r
	} })), Wt(e, r));
}
function Jt(e) {
	return Kt(e), Gt((t) => t.by[e] ?? Bt());
}
var Yt = /* @__PURE__ */ new Map();
function Xt(e, t) {
	qt(e, (n) => {
		let r = Date.now() / 1e3, i = !1, a = { ...n.players };
		for (let e of t.players ?? []) {
			let t = String(e.steamId ?? "");
			if (!/^\d{17}$/.test(t) || !e.name) continue;
			let n = a[t];
			(!n || n.name !== e.name || r - n.lastSeen > 300) && (a[t] = {
				steamId: t,
				name: e.name,
				firstSeen: n?.firstSeen ?? r,
				lastSeen: r
			}, i = !0);
		}
		let o = n.deaths, s = t.combatDeaths ?? [], c = s.length ? `${s.length}|${JSON.stringify(s[s.length - 1])}` : "";
		if (c && Yt.get(e) !== c) {
			Yt.set(e, c);
			let r = new Set(n.deaths.map((e) => e.key)), a = new Map((t.players ?? []).map((e) => [e.entity, e.name])), l = [];
			for (let e of s) {
				let t = `${e.entity}|${e.time}`;
				if (r.has(t)) continue;
				r.add(t);
				let n = e.killerName, i = e.killerKind;
				!n && a.has(e.killer) && (n = a.get(e.killer), i = "player"), l.push({
					...e,
					key: t,
					victimName: e.victimName ?? a.get(e.entity),
					killerName: n,
					killerKind: i ?? (e.killer ? void 0 : "none")
				});
			}
			l.length && (o = [...n.deaths, ...l].slice(-300), i = !0);
		}
		let l = n.graves;
		for (let e of t.deaths ?? []) {
			let n = `${e.entity}|${Math.round(e.clock * 10)}`;
			if (l.some((e) => e.key === n) || !e.pos) continue;
			let a = (t.players ?? []).find((t) => t.entity === e.entity)?.name ?? null;
			l = [...l, {
				key: n,
				entity: e.entity,
				name: a,
				pos: e.pos,
				at: r
			}].slice(-200), i = !0;
		}
		return i ? {
			...n,
			players: a,
			deaths: o,
			graves: l
		} : null;
	});
}
var Zt = !1;
function Qt() {
	Zt || (Zt = !0, vt(Xt));
}
function $t(e, t, n) {
	let r = t.trim();
	return r.length < 1 || r.length > 40 ? A("admin.history.waypointName") : [
		n.x,
		n.y,
		n.z
	].every((e) => typeof e == "number" && Number.isFinite(e)) ? (qt(e, (e) => ({
		...e,
		waypoints: [...e.waypoints.filter((e) => e.name.toLowerCase() !== r.toLowerCase()), {
			name: r,
			pos: {
				x: n.x,
				y: n.y,
				z: n.z
			},
			at: Date.now() / 1e3
		}]
	})), null) : A("admin.history.waypointNoPos");
}
function en(e, t) {
	qt(e, (e) => ({
		...e,
		waypoints: e.waypoints.filter((e) => e.name !== t)
	}));
}
function tn(e, t, n) {
	qt(e, (e) => ({
		...e,
		enemyLabels: {
			...e.enemyLabels,
			[t]: n.slice(0, 60)
		}
	}));
}
function nn(e, t = {}) {
	let n = new Map(Object.entries(e.players));
	for (let [e, r] of Object.entries(t)) !n.has(e) && /^\d{17}$/.test(e) && n.set(e, {
		steamId: e,
		name: r.name ?? "",
		firstSeen: 0,
		lastSeen: 0
	});
	return [...n.values()].sort((e, t) => t.lastSeen - e.lastSeen || e.name.localeCompare(t.name));
}
// src/shell/serverNav.ts
var rn = [
	["configs", "configs"],
	["mods", "mods"],
	["console", "console"]
], L = f(function({ variant: e = "secondary", size: t = "m", icon: n, loading: r, disabled: i, className: a, children: o, type: s = "button", ...c }, l) {
	let u = e === "molten" ? "primary" : e, d = [
		"k-btn",
		u !== "secondary" && `k-btn-${u}`,
		t !== "m" && `k-btn-${t}`,
		a
	].filter(Boolean).join(" "), f = o != null && o !== !1 && o !== "";
	return /* @__PURE__ */ E("button", {
		ref: l,
		type: s,
		className: d,
		disabled: i || r,
		"aria-busy": r || void 0,
		...c,
		children: [r ? /* @__PURE__ */ T(on, {}) : f ? null : n, o]
	});
}), an = f(function({ label: e, variant: t = "ghost", className: n, ...r }, i) {
	return /* @__PURE__ */ T(L, {
		ref: i,
		variant: t,
		"aria-label": e,
		title: e,
		className: ["k-btn-icon", n].filter(Boolean).join(" "),
		...r
	});
});
function on({ size: e }) {
	let t = M();
	return /* @__PURE__ */ T("span", {
		className: "k-spin",
		role: "status",
		"aria-label": t("ui.button.loading"),
		style: e ? {
			width: e,
			height: e
		} : void 0
	});
}
// src/ui/Form.tsx
function sn({ label: e, hint: t, error: n, badges: r, htmlFor: i, children: a }) {
	return /* @__PURE__ */ E("div", {
		className: "k-field",
		children: [
			(e || r) && /* @__PURE__ */ E("div", {
				className: "k-field-head",
				children: [e && /* @__PURE__ */ T("label", {
					className: "k-label",
					htmlFor: i,
					children: e
				}), r]
			}),
			a,
			n ? /* @__PURE__ */ T("div", {
				className: "k-error-text",
				children: n
			}) : t ? /* @__PURE__ */ T("div", {
				className: "k-hint",
				children: t
			}) : null
		]
	});
}
var R = f(function({ invalid: e, mono: t, className: n, ...r }, i) {
	return /* @__PURE__ */ T("input", {
		ref: i,
		className: [
			"k-input",
			t && "k-input-mono",
			e && "invalid",
			n
		].filter(Boolean).join(" "),
		"aria-invalid": e || void 0,
		...r
	});
});
f(function({ invalid: e, className: t, ...n }, r) {
	return /* @__PURE__ */ T("textarea", {
		ref: r,
		className: [
			"k-textarea",
			e && "invalid",
			t
		].filter(Boolean).join(" "),
		"aria-invalid": e || void 0,
		...n
	});
});
var z = f(function({ options: e, onChange: t, invalid: n, className: r, ...i }, a) {
	return /* @__PURE__ */ T("select", {
		ref: a,
		className: [
			"k-select",
			n && "invalid",
			r
		].filter(Boolean).join(" "),
		onChange: (e) => t(e.target.value),
		...i,
		children: e.map((e) => typeof e == "string" ? /* @__PURE__ */ T("option", {
			value: e,
			children: e
		}, e) : /* @__PURE__ */ T("option", {
			value: e.value,
			children: e.label
		}, e.value))
	});
});
function B({ value: e, onCommit: t, integer: n, min: r, max: i, step: a, invalid: o, className: s, ...c }) {
	let [l, u] = x(e == null ? "" : String(e)), d = b(e);
	g(() => {
		u(e == null ? "" : String(e)), d.current = e;
	}, [e]);
	let f = l.trim() === "" ? NaN : Number(l), p = Number.isNaN(f) || n && !Number.isInteger(f) || r !== void 0 && f < r || i !== void 0 && f > i, m = () => {
		!p && f !== e && f !== d.current && (d.current = f, t(f));
	};
	return /* @__PURE__ */ T("input", {
		className: [
			"k-input k-num",
			(o || p && l !== "") && "invalid",
			s
		].filter(Boolean).join(" "),
		inputMode: n ? "numeric" : "decimal",
		value: l,
		onChange: (e) => u(e.target.value),
		onBlur: m,
		onKeyDown: (t) => {
			t.key === "Enter" && m(), t.key === "Escape" && u(String(e ?? ""));
		},
		step: a,
		min: r,
		max: i,
		"aria-invalid": o || p || void 0,
		...c
	});
}
function cn({ value: e, min: t, max: n, step: r, integer: i, onCommit: a, disabled: o, id: s, ...c }) {
	let [l, u] = x(e), d = b(e);
	g(() => {
		u(e), d.current = e;
	}, [e]);
	let f = r ?? (i ? Math.max(1, Math.round((n - t) / 100)) : (n - t) / 100), p = n > t ? (l - t) / (n - t) * 100 : 0, m = () => {
		let t = i ? Math.round(l) : l;
		t !== e && t !== d.current && (d.current = t, a(t));
	};
	return /* @__PURE__ */ E("div", {
		className: "k-slider-row",
		children: [/* @__PURE__ */ T("input", {
			id: s,
			type: "range",
			className: "k-range",
			min: t,
			max: n,
			step: f,
			value: l,
			disabled: o,
			"aria-label": c["aria-label"],
			style: { backgroundSize: `${p}% 100%` },
			onChange: (e) => u(Number(e.target.value)),
			onPointerUp: m,
			onKeyUp: m,
			onBlur: m
		}), /* @__PURE__ */ T(B, {
			value: e,
			onCommit: a,
			integer: i,
			min: t,
			max: n,
			disabled: o,
			"aria-label": c["aria-label"]
		})]
	});
}
function ln({ checked: e, onChange: t, label: n, hint: r, disabled: i, id: a, ...o }) {
	let s = _();
	return /* @__PURE__ */ E("label", {
		className: "k-toggle" + (i ? " disabled" : ""),
		htmlFor: a ?? s,
		children: [
			/* @__PURE__ */ T("input", {
				id: a ?? s,
				type: "checkbox",
				role: "switch",
				checked: e,
				disabled: i,
				"aria-label": o["aria-label"],
				onChange: (e) => t(e.target.checked)
			}),
			/* @__PURE__ */ T("span", {
				className: "k-toggle-track",
				"aria-hidden": !0
			}),
			(n || r) && /* @__PURE__ */ E("span", {
				className: "k-toggle-text",
				children: [n && /* @__PURE__ */ T("span", {
					className: "k-label",
					children: n
				}), r && /* @__PURE__ */ T("span", {
					className: "k-hint",
					children: r
				})]
			})
		]
	});
}
function un(e) {
	if (e.key !== "Escape" || e.ctrlKey || e.altKey || e.metaKey || e.shiftKey || e.isComposing) return !1;
	let t = e.target;
	return !!t && typeof t.getAttribute == "function" && t.getAttribute("data-esc-clears") !== null && typeof t.value == "string" && t.value !== "";
}
var dn = (e, t) => typeof t == "string" ? e("ui.form.clearNamed", { label: t.toLowerCase() }) : e("ui.form.clear"), fn = f(function({ label: e, note: t, compact: n, wellStyle: r, id: i, className: a, onClear: o, clearLabel: s, onKeyDown: c, ...l }, u) {
	let d = M(), f = _(), p = i ?? f, m = b(null), h = (e) => {
		m.current = e, typeof u == "function" ? u(e) : u && (u.current = e);
	}, g = o !== void 0 && l.value !== void 0 && l.value !== null && String(l.value) !== "", v = () => {
		o?.(), m.current?.focus();
	}, y = (e) => {
		c?.(e), !e.defaultPrevented && o && un(e.nativeEvent) && (e.preventDefault(), e.stopPropagation(), o());
	}, x = s ?? dn(d, e);
	return /* @__PURE__ */ E("div", {
		className: [
			"k-well",
			n && "compact",
			a
		].filter(Boolean).join(" "),
		style: r,
		children: [
			/* @__PURE__ */ T("label", {
				className: "k-label",
				htmlFor: p,
				children: e
			}),
			/* @__PURE__ */ T("input", {
				ref: h,
				id: p,
				autoComplete: "off",
				spellCheck: !1,
				"data-esc-clears": o ? "" : void 0,
				onKeyDown: y,
				...l
			}),
			t != null && /* @__PURE__ */ T("span", {
				className: "k-well-note",
				children: t
			}),
			g && /* @__PURE__ */ T("button", {
				type: "button",
				className: "k-well-clear",
				"aria-label": x,
				title: d("ui.form.clearTitle", { word: x }),
				"aria-controls": p,
				onClick: v,
				children: /* @__PURE__ */ T(te, { "aria-hidden": !0 })
			})
		]
	});
}), pn = "button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex=\"-1\"])", mn = 0, hn = "k-covered", gn = typeof navigator < "u" && /Windows/i.test(navigator.userAgent);
function _n(e) {
	g(() => {
		if (e && (window.dispatchEvent(new Event(hn)), !gn)) return mn++, document.documentElement.setAttribute("data-covered", ""), () => {
			mn = Math.max(0, mn - 1), mn === 0 && document.documentElement.removeAttribute("data-covered");
		};
	}, [e]);
}
function vn({ open: e, onClose: t, title: n, children: r, footer: i, width: a, locked: o, arch: s }) {
	let c = M(), l = b(null), u = _(), d = b(t);
	if (d.current = t, _n(e), g(() => {
		if (!e) return;
		let t = document.activeElement;
		(l.current?.querySelector("[data-autofocus]") ?? l.current?.querySelector(pn))?.focus();
		let n = (e) => {
			if (e.key === "Escape" && !o && !un(e) && (e.stopPropagation(), d.current()), e.key === "Tab" && l.current) {
				let t = [...l.current.querySelectorAll(pn)];
				if (!t.length) return;
				let [n, r] = [t[0], t[t.length - 1]];
				e.shiftKey && document.activeElement === n ? (e.preventDefault(), r.focus()) : !e.shiftKey && document.activeElement === r && (e.preventDefault(), n.focus());
			}
		};
		return window.addEventListener("keydown", n, !0), () => {
			window.removeEventListener("keydown", n, !0), t?.focus?.();
		};
	}, [e, o]), !e) return null;
	let f = (e) => {
		e.target === e.currentTarget && !o && t();
	};
	return O(s ? /* @__PURE__ */ T("div", {
		className: "k-modal-backdrop",
		onMouseDown: f,
		children: /* @__PURE__ */ E("div", {
			ref: l,
			className: "k-arch",
			role: "dialog",
			"aria-modal": "true",
			"aria-labelledby": u,
			children: [/* @__PURE__ */ T("svg", {
				viewBox: "0 0 500 460",
				preserveAspectRatio: "none",
				"aria-hidden": !0,
				children: /* @__PURE__ */ T("path", { d: "M1 459V210C1 130 130 60 250 1C370 60 499 130 499 210V459Z" })
			}), /* @__PURE__ */ E("div", {
				className: "k-arch-in",
				children: [
					/* @__PURE__ */ T("h2", {
						id: u,
						className: "k-arch-title",
						children: n
					}),
					r,
					i && /* @__PURE__ */ T("div", {
						className: "k-arch-foot",
						children: i
					})
				]
			})]
		})
	}) : /* @__PURE__ */ T("div", {
		className: "k-modal-backdrop",
		onMouseDown: f,
		children: /* @__PURE__ */ E("div", {
			ref: l,
			className: "k-modal",
			role: "dialog",
			"aria-modal": "true",
			"aria-labelledby": u,
			style: { "--w": `${a ?? 520}px` },
			children: [
				/* @__PURE__ */ E("div", {
					className: "k-modal-head",
					children: [
						/* @__PURE__ */ T("h2", {
							className: "k-modal-title",
							id: u,
							children: n
						}),
						/* @__PURE__ */ T("span", { className: "k-spacer" }),
						!o && !i && /* @__PURE__ */ T(an, {
							label: c("common.close"),
							icon: /* @__PURE__ */ T(te, {}),
							size: "s",
							onClick: t
						})
					]
				}),
				/* @__PURE__ */ T("div", {
					className: "k-modal-body",
					children: r
				}),
				i && /* @__PURE__ */ T("div", {
					className: "k-modal-foot",
					children: i
				})
			]
		})
	}), document.body);
}
function yn({ children: e }) {
	return /* @__PURE__ */ T("div", {
		className: "k-arch-body",
		children: e
	});
}
var bn = "kiln.confirm.typeNames";
function xn() {
	try {
		return localStorage.getItem(bn) === "1";
	} catch {
		return !1;
	}
}
function Sn({ open: e, onClose: t, title: n, message: r, word: i, action: a, onConfirm: o, disabled: s, relaxable: c }) {
	let l = M(), [u, d] = x(""), [f, p] = x(!1), [m, h] = x(!0);
	g(() => {
		e && (d(""), p(!1), h(!c || xn()));
	}, [e, c]);
	let _ = (!m || u.trim() === i) && !s, v = async () => {
		if (_ && !f) {
			p(!0);
			try {
				await o();
			} catch {
				return;
			} finally {
				p(!1);
			}
			t();
		}
	};
	return /* @__PURE__ */ E(vn, {
		arch: !0,
		open: e,
		onClose: t,
		title: n,
		locked: f,
		footer: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(L, {
			onClick: t,
			disabled: f,
			children: l("common.cancel")
		}), /* @__PURE__ */ T(L, {
			variant: "danger-fill",
			disabled: !_,
			loading: f,
			onClick: v,
			children: a
		})] }),
		children: [r && /* @__PURE__ */ T(yn, { children: r }), m && /* @__PURE__ */ E("div", {
			className: "k-arch-typed",
			children: [/* @__PURE__ */ T("label", {
				htmlFor: "k-confirm-word",
				children: j("ui.modal.typeToConfirm", { word: /* @__PURE__ */ T("span", {
					className: "k-danger-word",
					children: i
				}) })
			}), /* @__PURE__ */ T(R, {
				id: "k-confirm-word",
				"data-autofocus": !0,
				value: u,
				onChange: (e) => d(e.target.value),
				mono: !0,
				autoComplete: "off",
				spellCheck: !1,
				onKeyDown: (e) => {
					e.key === "Enter" && v();
				}
			})]
		})]
	});
}
// src/ui/StatusPill.tsx
function V({ tone: e = "idle", title: t, children: n }) {
	return /* @__PURE__ */ T("span", {
		className: `k-badge k-tone-${e}`,
		title: t,
		children: Cn(n)
	});
}
function Cn(e) {
	return typeof e == "string" && e.length ? e.charAt(0).toUpperCase() + e.slice(1) : e;
}
var wn = "kiln.banner.folded.";
function Tn(e) {
	if (!e) return !1;
	try {
		return localStorage.getItem(wn + e) === "1";
	} catch {
		return !1;
	}
}
function H({ tone: e = "info", title: t, actions: n, children: r, fold: i }) {
	let a = M(), [o, s] = x(() => Tn(i));
	return g(() => s(Tn(i)), [i]), /* @__PURE__ */ E("div", {
		className: "k-banner" + (o ? " folded" : ""),
		role: e === "err" ? "alert" : "status",
		children: [/* @__PURE__ */ E("div", {
			className: "k-banner-body",
			children: [t && /* @__PURE__ */ T("div", {
				className: `k-banner-title k-tone-${e}`,
				children: t
			}), r && !o && /* @__PURE__ */ T("div", {
				className: "k-banner-text",
				children: r
			})]
		}), (n || i) && /* @__PURE__ */ E("div", {
			className: "k-banner-actions",
			children: [n, i && /* @__PURE__ */ T("button", {
				type: "button",
				className: "k-link k-banner-fold",
				"aria-expanded": !o,
				onClick: () => {
					let e = !o;
					s(e);
					try {
						e ? localStorage.setItem(wn + i, "1") : localStorage.removeItem(wn + i);
					} catch {}
				},
				children: a(o ? "ui.banner.show" : "ui.banner.hide")
			})]
		})]
	});
}
// src/ui/Toast.tsx
var En = 8, Dn = (e) => e == null || e === !1 ? "" : typeof e == "string" || typeof e == "number" ? String(e) : null, On = (e, t) => {
	let n = Dn(e.title), r = Dn(e.detail);
	return e.kind === t.kind && n !== null && r !== null && n === Dn(t.title) && r === Dn(t.detail);
}, kn = (e) => (e.count ?? 1) > 1 ? A("ui.toast.times", { count: e.count }) : "";
function An({ items: e }) {
	return M(), /* @__PURE__ */ T(w, { children: e.map((e) => /* @__PURE__ */ E("div", {
		className: "k-toast-earlier",
		"data-kind": e.kind,
		children: [
			/* @__PURE__ */ E("span", {
				className: "k-toast-earlier-title",
				children: [e.title, kn(e)]
			}),
			!!e.detail && /* @__PURE__ */ E(w, { children: [" ", e.detail] }),
			!!e.actions?.length && /* @__PURE__ */ T("span", {
				className: "k-toast-actions",
				children: e.actions.map((t) => /* @__PURE__ */ T("button", {
					type: "button",
					className: "k-btn k-btn-s",
					onClick: () => {
						Pn.getState().close(e.id), t.onClick();
					},
					children: t.label
				}, t.label))
			})
		]
	}, e.id)) });
}
var jn = (e, t) => ({
	id: t,
	kind: e.some((e) => e.kind === "err") ? "err" : "info",
	persistent: !0,
	folded: e,
	title: A("ui.toast.earlier", { count: e.length }),
	detail: /* @__PURE__ */ T(An, { items: e })
});
function Mn(e, t, n = []) {
	return e.flatMap((e) => {
		if (!e.folded) return t(e) ? (n.push(e), []) : [e];
		if (t(e)) return n.push(e), [];
		let r = e.folded.filter((e) => !t(e) || (n.push(e), !1));
		return r.length === e.folded.length ? [e] : r.length ? [jn(r, e.id)] : [];
	});
}
var Nn = 0, Pn = C((e, t) => ({
	items: [],
	push: (n, r) => {
		let i = ++Nn, a = r ?? (n.kind === "err" ? 0 : n.actions?.length ? 8e3 : 2600), o = a === 0, s = [], c = o ? Mn(t().items, (e) => !!e.persistent && !e.folded && On(e, n), s) : t().items, l = s.reduce((e, t) => e + (t.count ?? 1), 1), u = [...s.map((e) => e.onClosed), n.onClosed].filter((e) => !!e), d = [.../* @__PURE__ */ new Set([...s.flatMap((e) => e.lines ?? []), ...n.lines ?? []])], f = {
			onClosed: u.length ? () => u.forEach((e) => e()) : void 0,
			lines: d.length ? d : void 0
		};
		c = [...c, {
			...n,
			...f,
			id: i,
			persistent: o,
			...l > 1 ? { count: l } : {}
		}];
		let p = c.find((e) => e.folded), m = c.filter((e) => e.persistent && !e.folded);
		if (m.length + +!!p > En) {
			let e = new Set(m.slice(0, m.length - 7).map((e) => e.id));
			c = [jn([...p?.folded ?? [], ...m.filter((t) => e.has(t.id))], p?.id ?? ++Nn), ...c.filter((t) => t !== p && !e.has(t.id))];
		}
		let h = new Set(c.filter((e) => !e.persistent).slice(-4).map((e) => e.id));
		return e({ items: c.filter((e) => e.persistent || h.has(e.id)) }), a > 0 && setTimeout(() => t().dismiss(i), a), i;
	},
	dismiss: (n) => e({ items: Mn(t().items, (e) => e.id === n) }),
	close: (n) => {
		let r = [];
		e({ items: Mn(t().items, (e) => e.id === n, r) });
		for (let e of r) for (let t of [e, ...e.folded ?? []]) t.onClosed?.();
	}
})), Fn = {
	ok: (e, t, n = {}) => Pn.getState().push({
		kind: "ok",
		title: e,
		detail: t,
		actions: n.actions,
		onClosed: n.onClosed,
		lines: n.lines
	}, n.ms),
	info: (e, t, n = {}) => Pn.getState().push({
		kind: "info",
		title: e,
		detail: t,
		actions: n.actions,
		onClosed: n.onClosed,
		lines: n.lines
	}, n.ms),
	error: (e, t) => {
		let n = Ge(e);
		return Pn.getState().push({
			kind: "err",
			title: t ?? n.message,
			detail: t ? n.message : n.code === "internal" ? void 0 : n.code
		});
	},
	dismiss: (e) => Pn.getState().dismiss(e)
};
function In({ children: e }) {
	let t = M(), n = b(null), [r, i] = x(!1), [a, o] = x(!1);
	return v(() => {
		let e = n.current;
		e && !a && i(e.scrollHeight > e.clientHeight + 1);
	}, [e, a]), /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T("div", {
		ref: n,
		className: "k-toast-detail",
		"data-open": a ? "" : void 0,
		"data-cut": r && !a ? "" : void 0,
		children: e
	}), (r || a) && /* @__PURE__ */ T("button", {
		type: "button",
		className: "k-link k-toast-more",
		"aria-expanded": a,
		onClick: () => o(!a),
		children: t(a ? "ui.toast.showLess" : "ui.toast.showAll")
	})] });
}
function Ln() {
	let e = M(), { items: t, close: n } = Pn(), r = b(null), i = t.reduce((e, t) => Math.max(e, t.id), 0);
	return v(() => {
		r.current && (r.current.scrollTop = r.current.scrollHeight);
	}, [i]), /* @__PURE__ */ T("div", {
		ref: r,
		className: "k-toasts",
		"aria-live": "polite",
		children: t.map((t) => /* @__PURE__ */ E("div", {
			className: `k-toast k-toast-${t.kind}`,
			role: t.kind === "err" ? "alert" : "status",
			children: [
				/* @__PURE__ */ E("div", {
					className: "k-toast-body",
					children: [/* @__PURE__ */ E("div", {
						className: "k-toast-title",
						children: [t.title, kn(t)]
					}), t.detail && /* @__PURE__ */ T(In, { children: t.detail })]
				}),
				!!t.actions?.length && /* @__PURE__ */ T("div", {
					className: "k-toast-actions",
					children: t.actions.map((e) => /* @__PURE__ */ T("button", {
						type: "button",
						className: "k-btn k-btn-s",
						onClick: () => {
							n(t.id), e.onClick();
						},
						children: e.label
					}, e.label))
				}),
				/* @__PURE__ */ T("button", {
					type: "button",
					className: "k-toast-close",
					"aria-label": e("common.dismiss"),
					title: e("common.dismiss"),
					onClick: () => n(t.id),
					children: /* @__PURE__ */ T(te, { size: 14 })
				})
			]
		}, t.id))
	});
}
// src/ui/Card.tsx
var Rn = u(2);
function U({ title: e, sub: t, actions: n, footer: r, glow: i, flush: a, className: o, children: s, ...c }) {
	let l = h(Rn) === 3 ? "h3" : "h2";
	return /* @__PURE__ */ E("section", {
		className: ["k-card", o].filter(Boolean).join(" "),
		...c,
		children: [
			(e || t || n) && /* @__PURE__ */ E("header", {
				className: "k-card-head",
				children: [/* @__PURE__ */ E("div", {
					className: "k-card-titles",
					children: [e && /* @__PURE__ */ T(l, {
						className: "k-card-title",
						children: e
					}), t && /* @__PURE__ */ T("div", {
						className: "k-card-sub",
						children: t
					})]
				}), n && /* @__PURE__ */ T("div", {
					className: "k-card-actions",
					children: n
				})]
			}),
			/* @__PURE__ */ T("div", {
				className: "k-card-body",
				children: s
			}),
			r && /* @__PURE__ */ T("footer", {
				className: "k-card-foot",
				children: r
			})
		]
	});
}
function zn({ title: e, sub: t, actions: n, meta: r }) {
	return /* @__PURE__ */ E("header", {
		className: "k-page-head",
		children: [/* @__PURE__ */ E("div", { children: [/* @__PURE__ */ E("div", {
			className: "k-page-name",
			children: [/* @__PURE__ */ T("h1", {
				className: "k-page-title",
				children: e
			}), r && /* @__PURE__ */ T("span", {
				className: "k-page-meta",
				children: r
			})]
		}), t && /* @__PURE__ */ T("p", {
			className: "k-page-sub",
			children: t
		})] }), n && /* @__PURE__ */ T("div", {
			className: "k-page-actions",
			children: n
		})]
	});
}
function Bn({ wide: e, fill: t, className: n, children: r }) {
	return /* @__PURE__ */ T("div", {
		className: [
			"k-page",
			e && "k-page-wide",
			t && "k-page-fill",
			n
		].filter(Boolean).join(" "),
		children: r
	});
}
function Vn({ children: e, aside: t, form: n }) {
	return /* @__PURE__ */ E("div", {
		className: "k-split-page",
		children: [/* @__PURE__ */ T("div", {
			className: "k-split-main",
			children: e
		}), /* @__PURE__ */ T("aside", {
			className: "k-aside" + (n ? " k-aside-form" : ""),
			children: t
		})]
	});
}
function Hn({ rows: e }) {
	return /* @__PURE__ */ T("dl", {
		className: "k-kv",
		children: e.filter(Boolean).map((e, t) => {
			let [n, r] = e;
			return /* @__PURE__ */ E("div", {
				style: { display: "contents" },
				children: [/* @__PURE__ */ T("dt", { children: n }), /* @__PURE__ */ T("dd", { children: r })]
			}, t);
		})
	});
}
// src/ui/EmptyState.tsx
function W({ title: e, hint: t, action: n, page: r }) {
	return /* @__PURE__ */ E("div", {
		className: "k-empty" + (r ? " page" : ""),
		children: [
			/* @__PURE__ */ T("svg", {
				className: "k-empty-arch",
				viewBox: "0 0 56 72",
				"aria-hidden": !0,
				children: /* @__PURE__ */ T("path", { d: "M1 71V31C1 18 15 8 28 1C41 8 55 18 55 31V71Z" })
			}),
			/* @__PURE__ */ T("div", {
				className: "k-empty-title",
				children: e
			}),
			t && /* @__PURE__ */ T("div", {
				className: "k-empty-hint",
				children: t
			}),
			n && /* @__PURE__ */ T("div", {
				className: "k-empty-action",
				children: n
			})
		]
	});
}
// src/ui/Tabs.tsx
function Un({ items: e, value: t, onChange: n, ...r }) {
	let i = b(null), a = (r) => {
		let a = e.filter((e) => !e.disabled), o = a[(a.findIndex((e) => e.id === t) + r + a.length) % a.length];
		o && (n(o.id), requestAnimationFrame(() => i.current?.querySelector("[aria-selected=\"true\"]")?.focus()));
	};
	return /* @__PURE__ */ T("div", {
		ref: i,
		className: "k-tabs",
		role: "tablist",
		"aria-label": r["aria-label"],
		onKeyDown: (e) => {
			e.key === "ArrowRight" && a(1), e.key === "ArrowLeft" && a(-1);
		},
		children: e.map((e) => /* @__PURE__ */ E("button", {
			role: "tab",
			type: "button",
			"aria-selected": e.id === t,
			tabIndex: e.id === t ? 0 : -1,
			disabled: e.disabled,
			className: "k-tab" + (e.id === t ? " active" : ""),
			onClick: () => n(e.id),
			children: [e.label, e.count !== void 0 && /* @__PURE__ */ T("span", {
				className: "k-tab-count",
				children: e.count
			})]
		}, e.id))
	});
}
function Wn({ options: e, value: t, onChange: n, variant: r = "line", ...i }) {
	let a = b(null), [o, s] = x(null);
	return v(() => {
		if (r !== "side") return;
		let e = () => {
			let e = a.current?.querySelector("button.on");
			s(e ? e.offsetLeft + e.offsetWidth / 2 : null);
		};
		e(), document.fonts?.ready.then(e).catch(() => {});
	}, [
		t,
		r,
		e.length
	]), /* @__PURE__ */ E("div", {
		ref: a,
		className: r === "side" ? "k-side" : "k-seg",
		role: "radiogroup",
		"aria-label": i["aria-label"],
		onKeyDown: (r) => {
			let i = e.findIndex((e) => e.id === t), o = r.key === "ArrowRight" || r.key === "ArrowDown" ? 1 : r.key === "ArrowLeft" || r.key === "ArrowUp" ? -1 : 0;
			if (!o) return;
			r.preventDefault();
			let s = e[(i + o + e.length) % e.length];
			n(s.id), requestAnimationFrame(() => a.current?.querySelector("button.on")?.focus());
		},
		children: [e.map((e) => /* @__PURE__ */ T("button", {
			type: "button",
			role: "radio",
			"aria-checked": e.id === t,
			tabIndex: e.id === t ? 0 : -1,
			className: e.id === t ? "on" : "",
			onClick: () => n(e.id),
			children: e.label
		}, e.id)), r === "side" && o !== null && /* @__PURE__ */ T("span", {
			className: "k-side-dia",
			"aria-hidden": !0,
			style: { transform: `translateX(${o - 3.5}px)` },
			children: /* @__PURE__ */ T("i", { className: "k-dia" })
		})]
	});
}
// src/ui/Table.tsx
function Gn({ columns: e, rows: t, rowKey: n, rowHeight: r = 46, height: i, maxHeight: a = 520, fill: o, onRowClick: s, selectedKey: c, rowClass: l, empty: u, ...d }) {
	let f = M(), p = b(null), [m, h] = x(null), g = y(() => {
		let n = m && e.find((e) => e.key === m.key);
		return n?.sort ? [...t].sort((e, t) => n.sort(e, t) * m.dir) : t;
	}, [
		t,
		m,
		e
	]), _ = ne({
		count: g.length,
		getScrollElement: () => p.current,
		estimateSize: () => r,
		overscan: 8
	}), v = e.map((e) => e.width ?? "minmax(0, 1fr)").join(" "), S = o ? void 0 : i ?? Math.min(a, Math.max(g.length, 1) * r), C = (e, t) => {
		e.target === e.currentTarget && s && (e.key === "Enter" || e.key === " ") && (e.preventDefault(), s(t));
	};
	return /* @__PURE__ */ E("div", {
		className: "k-table" + (o ? " k-table-fill" : ""),
		role: "table",
		"aria-label": d["aria-label"],
		"aria-rowcount": g.length,
		children: [/* @__PURE__ */ T("div", {
			className: "k-table-head",
			role: "row",
			style: { gridTemplateColumns: v },
			children: e.map((e) => /* @__PURE__ */ T("div", {
				role: "columnheader",
				className: "k-table-cell" + (e.align === "right" ? " right" : ""),
				"aria-sort": m?.key === e.key ? m.dir === 1 ? "ascending" : "descending" : void 0,
				children: e.sort ? /* @__PURE__ */ E("button", {
					type: "button",
					onClick: () => h((t) => t?.key === e.key ? t.dir === 1 ? {
						key: e.key,
						dir: -1
					} : null : {
						key: e.key,
						dir: 1
					}),
					children: [e.header, m?.key === e.key && /* @__PURE__ */ T("i", {
						className: "k-dia",
						"aria-hidden": !0,
						title: m.dir === 1 ? f("ui.table.ascending") : f("ui.table.descending")
					})]
				}) : e.header
			}, e.key))
		}), g.length === 0 ? /* @__PURE__ */ T("div", {
			className: "k-table-empty",
			children: u ?? /* @__PURE__ */ T("span", {
				className: "k-empty-title",
				children: f("ui.table.empty")
			})
		}) : /* @__PURE__ */ T("div", {
			ref: p,
			className: "k-table-scroll",
			style: { height: S },
			children: /* @__PURE__ */ T("div", {
				style: {
					height: _.getTotalSize(),
					position: "relative"
				},
				children: _.getVirtualItems().map((t) => {
					let i = g[t.index], a = n(i, t.index);
					return /* @__PURE__ */ T("div", {
						role: "row",
						"aria-rowindex": t.index + 1,
						tabIndex: s ? 0 : void 0,
						"aria-selected": s ? c === a : void 0,
						className: "k-table-row" + (s ? " clickable" : "") + (c === a ? " selected" : "") + (l?.(i) ? ` ${l(i)}` : ""),
						style: {
							gridTemplateColumns: v,
							height: r,
							transform: `translateY(${t.start}px)`
						},
						onClick: s ? () => s(i) : void 0,
						onKeyDown: (e) => C(e, i),
						children: e.map((e) => /* @__PURE__ */ T("div", {
							role: "cell",
							className: "k-table-cell" + (e.align === "right" ? " right" : ""),
							children: e.render ? e.render(i, t.index) : String(i[e.key] ?? "")
						}, e.key))
					}, a);
				})
			})
		})]
	});
}
function Kn({ name: e, id: t, dim: n }) {
	return /* @__PURE__ */ E("span", {
		className: "k-name-id",
		style: n ? { opacity: .6 } : void 0,
		children: [/* @__PURE__ */ T("span", { children: e }), t != null && t !== e && /* @__PURE__ */ T("span", {
			className: "k-mono",
			children: t
		})]
	});
}
// src/ui/JsonView.tsx
function qn({ value: e, open: t = 1, maxHeight: n = 420 }) {
	return /* @__PURE__ */ T("div", {
		className: "k-json",
		style: { maxHeight: n },
		children: /* @__PURE__ */ T(Yn, {
			value: e,
			depth: 0,
			open: t
		})
	});
}
function Jn({ value: e }) {
	return e == null ? /* @__PURE__ */ T("span", {
		className: "nul",
		children: "null"
	}) : typeof e == "string" ? /* @__PURE__ */ E("span", {
		className: "str",
		children: [
			"\"",
			e,
			"\""
		]
	}) : typeof e == "number" ? /* @__PURE__ */ T("span", {
		className: "num",
		children: e
	}) : typeof e == "boolean" ? /* @__PURE__ */ T("span", {
		className: "bool",
		children: String(e)
	}) : /* @__PURE__ */ T("span", { children: String(e) });
}
function Yn({ value: e, depth: t, open: n, label: r }) {
	let i = M(), a = Array.isArray(e);
	if (typeof e != "object" || !e) return /* @__PURE__ */ E("div", { children: [r, /* @__PURE__ */ T(Jn, { value: e })] });
	let o = a ? e.map((e, t) => [String(t), e]) : Object.entries(e), [s, c] = a ? ["[", "]"] : ["{", "}"];
	return o.length === 0 ? /* @__PURE__ */ E("div", { children: [r, /* @__PURE__ */ E("span", {
		className: "meta",
		children: [s, c]
	})] }) : /* @__PURE__ */ E("details", {
		open: t < n,
		children: [/* @__PURE__ */ E("summary", { children: [r, /* @__PURE__ */ E("span", {
			className: "meta",
			children: [
				s,
				" ",
				i(a ? "ui.jsonView.items" : "ui.jsonView.keys", { n: o.length }),
				" ",
				c
			]
		})] }), /* @__PURE__ */ T("div", {
			className: "ind",
			children: o.map(([e, r]) => /* @__PURE__ */ T(Yn, {
				value: r,
				depth: t + 1,
				open: n,
				label: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T("span", {
					className: a ? "meta" : "key",
					children: a ? e : `"${e}"`
				}), ": "] })
			}, e))
		})]
	});
}
// src/ui/SchemaForm.tsx
var Xn = /^(\d+(\.\d+)?(ms|s|m|h|d))+$/, Zn = {
	ms: .001,
	s: 1,
	m: 60,
	h: 3600,
	d: 86400
};
function Qn(e) {
	let t = e.trim();
	if (!Xn.test(t)) return null;
	let n = 0;
	for (let e of t.matchAll(/(\d+(?:\.\d+)?)(ms|s|m|h|d)/g)) n += Number(e[1]) * Zn[e[2]];
	return n;
}
function $n(e, t) {
	let n = e;
	for (let e of t.split(".")) {
		let [t, ...r] = e.split("[");
		t && (n = n && typeof n == "object" && !Array.isArray(n) ? n[t] : void 0);
		for (let e of r) n = Array.isArray(n) ? n[Number(e.replace("]", ""))] : void 0;
		if (n === void 0) return;
	}
	return n;
}
function er(e) {
	let t = e.step, { min: n, max: r } = e;
	if (!(e.type !== "int" && e.type !== "float" || typeof t != "number" || typeof n != "number" || typeof r != "number") && !(!Number.isFinite(t) || !Number.isFinite(n) || !Number.isFinite(r) || t <= 0 || r <= n || t > r - n) && n + t !== n && r - t !== r && (e.type !== "int" || Number.isInteger(t) && Number.isInteger(n))) return t;
}
function tr(e, t) {
	let n = e.type;
	return n === "bool" ? typeof t == "boolean" ? null : A("ui.schemaForm.notOnOff") : n === "int" || n === "float" ? typeof t != "number" || Number.isNaN(t) ? A("ui.schemaForm.notNumber") : n === "int" && !Number.isInteger(t) ? A("ui.schemaForm.wholeOnly") : e.min !== void 0 && t < e.min ? A("ui.schemaForm.min", { min: e.min }) : e.max !== void 0 && t > e.max ? A("ui.schemaForm.max", { max: e.max }) : null : n === "enum" ? typeof t == "string" && (!e.options || e.options.includes(t)) ? null : A("ui.schemaForm.notOption") : n === "duration" ? typeof t == "string" && Qn(t) !== null ? null : A("ui.schemaForm.durationLike") : n.startsWith("list<") ? Array.isArray(t) ? null : A("ui.schemaForm.notList") : typeof t == "string" ? null : A("ui.schemaForm.notText");
}
var nr = (e, t) => JSON.stringify(e) === JSON.stringify(t), rr = (e) => {
	let t = (e.split(".").pop() ?? e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
	return t.charAt(0).toUpperCase() + t.slice(1);
};
function ir({ schema: e, values: t, onChange: n, onReset: r, errors: i = [], pending: a = [], disabled: o, filter: s, labels: c, clamp: l }) {
	let u = M(), d = y(() => {
		let t = [...e.sections ?? []].sort((e, t) => t.path.length - e.path.length), n = s?.trim().toLowerCase(), r = e.entries.filter((e) => !n || e.path.toLowerCase().includes(n) || (e.comment ?? "").toLowerCase().includes(n)), i = /* @__PURE__ */ new Map();
		for (let e of r) {
			let n = t.find((t) => e.path.startsWith(t.path + "."))?.path ?? "";
			i.set(n, [...i.get(n) ?? [], e]);
		}
		let a = ["", ...(e.sections ?? []).map((e) => e.path)];
		return a.filter((e, t) => i.has(e) && a.indexOf(e) === t).map((t) => ({
			section: (e.sections ?? []).find((e) => e.path === t) ?? null,
			entries: i.get(t)
		}));
	}, [e, s]);
	return d.length === 0 ? /* @__PURE__ */ T("div", {
		className: "k-empty-title",
		children: u(s ? "ui.schemaForm.noMatch" : "ui.schemaForm.noSettings")
	}) : /* @__PURE__ */ T("div", {
		className: "k-sform",
		children: d.map((e) => /* @__PURE__ */ E("div", {
			className: "k-sform-section",
			children: [e.section && /* @__PURE__ */ E("div", {
				className: "k-sform-section-head",
				children: [
					/* @__PURE__ */ T("span", {
						className: "k-sform-section-title",
						children: rr(e.section.path)
					}),
					e.section.synced && /* @__PURE__ */ T("span", {
						className: "k-word-sm k-tone-info",
						title: u("ui.schemaForm.syncedTitle"),
						children: u("ui.schemaForm.synced")
					}),
					e.section.comment && /* @__PURE__ */ T("span", {
						className: "k-hint",
						children: e.section.comment
					})
				]
			}), e.entries.map((e) => /* @__PURE__ */ T(ar, {
				entry: e,
				value: $n(t, e.path),
				onChange: n,
				onReset: r,
				disabled: o,
				error: i.find((t) => t.path === e.path)?.message,
				pending: a.includes(e.path),
				name: c?.[e.path],
				clamp: l
			}, e.path))]
		}, e.section?.path ?? "_"))
	});
}
function ar({ entry: e, value: t, onChange: n, onReset: r, error: i, pending: a, disabled: o, name: s, clamp: c }) {
	let l = M(), u = s ?? rr(e.path), d = t === void 0 ? e.default : t, f = t !== void 0 && !nr(t, e.default), p = tr(e, d), m = i ?? (f ? p : null), h = `sf-${e.path.replace(/[^a-z0-9]/gi, "-")}`, _ = (t) => {
		nr(t, d) || n(e.path, t);
	}, v = b(null), y = e.comment ? `${h}-help` : null;
	return g(() => {
		let e = v.current?.querySelector(`#${CSS.escape(h)}`);
		e && (y ? e.setAttribute("aria-describedby", y) : e.getAttribute("aria-describedby")?.endsWith("-help") && e.removeAttribute("aria-describedby"));
	}), /* @__PURE__ */ E("div", {
		className: "k-sform-row" + (f ? " changed" : "") + (m ? " invalid" : ""),
		ref: v,
		children: [/* @__PURE__ */ E("div", {
			className: "k-sform-meta",
			children: [
				/* @__PURE__ */ E("div", {
					className: "k-sform-label",
					children: [
						/* @__PURE__ */ T("label", {
							htmlFor: h,
							children: u
						}),
						e.requiresRestart && /* @__PURE__ */ T("span", {
							className: "k-word-sm k-tone-warn",
							title: l("ui.schemaForm.restartTitle"),
							children: l("ui.schemaForm.restart")
						}),
						e.synced && /* @__PURE__ */ T("span", {
							className: "k-word-sm k-tone-info",
							title: l("ui.schemaForm.syncedTitle"),
							children: l("ui.schemaForm.synced")
						}),
						a && /* @__PURE__ */ T("span", {
							className: "k-word-sm k-tone-ember",
							title: l("ui.schemaForm.pendingTitle"),
							children: l("ui.schemaForm.pending")
						})
					]
				}),
				e.comment && /* @__PURE__ */ T(mr, {
					id: `${h}-help`,
					text: e.comment,
					clamp: c,
					label: u
				}),
				/* @__PURE__ */ T("span", {
					className: "k-sform-key",
					children: e.path
				})
			]
		}), /* @__PURE__ */ E("div", {
			className: "k-sform-control",
			children: [
				/* @__PURE__ */ T(gr, {
					id: h,
					entry: e,
					value: d,
					set: _,
					disabled: o,
					invalid: !!m
				}),
				m && /* @__PURE__ */ T("span", {
					className: "k-error-text",
					children: m
				}),
				f && /* @__PURE__ */ E("span", {
					className: "k-sform-default",
					children: [j("ui.schemaForm.default", { value: /* @__PURE__ */ T("span", {
						className: "k-mono",
						children: hr(e.default)
					}) }), r && /* @__PURE__ */ E(w, { children: [" · ", /* @__PURE__ */ T("button", {
						type: "button",
						className: "k-link",
						"aria-label": l("ui.schemaForm.resetLabel", { name: u }),
						onClick: () => r(e.path),
						disabled: o,
						children: l("common.reset")
					})] })]
				})
			]
		})]
	});
}
var or = 90, sr = (e) => e.normalize("NFD").replace(/\p{M}+/gu, "").toLocaleLowerCase().replace(/[^\p{L}\p{N}]+/gu, " ").trim(), cr = (e) => e.charAt(0).toLocaleUpperCase() + e.slice(1);
function lr(e, t, n = 0) {
	let r = 0;
	for (let i = 0; i < e.length; i++) {
		let a = e[i];
		if (a === "(" ? r++ : a === ")" && (r = Math.max(0, r - 1)), r === 0 && i >= n && e.startsWith(t, i)) return i;
	}
	return -1;
}
var ur = (e) => {
	let t = "", n = 0;
	for (let r of e) r === "(" ? n++ : r === ")" ? n = Math.max(0, n - 1) : n === 0 && (t += r);
	return t.replace(/\s+([,.;:])/g, "$1").replace(/\s{2,}/g, " ").trim();
};
function dr(e, t) {
	let n = sr(e);
	if (/^with\b/.test(n) || e.trim().split(/\s+/).length <= 3) return !0;
	let r = t ? sr(t) : "";
	return !!r && (n === r || n.endsWith(` ${r}`));
}
function fr(e, t) {
	if (e.length <= or) return e;
	let n = ur(e), r = (e) => sr(e).replace(/^the /, "");
	if (n.length <= or && (!t || r(n) !== r(t))) return n;
	let i = -1;
	for (let e of [
		": ",
		". ",
		"; "
	]) {
		let t = lr(n, e, 12);
		t >= 12 && t <= or && (i < 0 || t < i) && (i = t);
	}
	if (i > 0) return n.slice(0, i) + (n[i] === "." ? "." : "");
	let a = n.lastIndexOf(" ", 80);
	return `${n.slice(0, a > 20 ? a : 80)}…`;
}
function pr(e, t) {
	if (e.length <= or) return null;
	let n = lr(e, ": "), r = fr(n > 0 && dr(e.slice(0, n), t) ? cr(e.slice(n + 2).trim()) : e, t);
	return r === e ? null : r;
}
function mr({ id: e, text: t, clamp: n, label: r }) {
	let i = M(), [a, o] = x(!1), s = n ? pr(t, r) : null;
	return s ? /* @__PURE__ */ E("span", {
		className: "k-hint",
		children: [/* @__PURE__ */ T("span", {
			id: e,
			children: a ? t : s
		}), /* @__PURE__ */ T("button", {
			type: "button",
			className: "k-link k-sform-more",
			"aria-expanded": a,
			onClick: () => o(!a),
			children: i(a ? "ui.schemaForm.less" : "ui.schemaForm.more")
		})]
	}) : /* @__PURE__ */ T("span", {
		className: "k-hint",
		id: e,
		children: t
	});
}
function hr(e) {
	return typeof e == "string" ? `"${e}"` : JSON.stringify(e);
}
function gr({ id: e, entry: t, value: n, set: r, disabled: i, invalid: a }) {
	let o = t.type;
	if (o === "bool") return /* @__PURE__ */ T(ln, {
		id: e,
		checked: n === !0,
		onChange: r,
		disabled: i,
		"aria-label": t.path
	});
	if (o === "int" || o === "float") {
		let s = typeof n == "number" ? n : Number(n);
		return t.min !== void 0 && t.max !== void 0 && t.max > t.min && t.max - t.min <= 1e5 ? /* @__PURE__ */ T(cn, {
			id: e,
			value: s,
			min: t.min,
			max: t.max,
			step: er(t),
			integer: o === "int",
			onCommit: r,
			disabled: i,
			"aria-label": t.path
		}) : /* @__PURE__ */ T(B, {
			id: e,
			value: Number.isNaN(s) ? null : s,
			integer: o === "int",
			min: t.min,
			max: t.max,
			onCommit: r,
			disabled: i,
			invalid: a
		});
	}
	if (o === "enum") {
		let o = t.options ?? [], s = typeof n == "string" ? n : "";
		return /* @__PURE__ */ T(z, {
			id: e,
			value: s,
			options: o.includes(s) ? o : [s, ...o],
			onChange: r,
			disabled: i,
			invalid: a
		});
	}
	return o === "duration" ? /* @__PURE__ */ T(_r, {
		id: e,
		value: typeof n == "string" ? n : String(n ?? ""),
		onCommit: r,
		disabled: i,
		invalid: a,
		placeholder: "5m",
		mono: !0
	}) : o.startsWith("list<") ? /* @__PURE__ */ T(yr, {
		id: e,
		inner: o.slice(5, -1),
		value: Array.isArray(n) ? n : [],
		onCommit: r,
		disabled: i
	}) : /* @__PURE__ */ T(_r, {
		id: e,
		value: typeof n == "string" ? n : n === null ? "" : String(n),
		onCommit: r,
		disabled: i,
		invalid: a
	});
}
function _r({ value: e, onCommit: t, ...n }) {
	let [r, i] = x(e), [a, o] = x(e);
	return a !== e && (o(e), i(e)), /* @__PURE__ */ T(R, {
		...n,
		value: r,
		onChange: (e) => i(e.target.value),
		onBlur: () => r !== e && t(r),
		onKeyDown: (n) => {
			n.key === "Enter" && t(r), n.key === "Escape" && i(e);
		},
		spellCheck: !1
	});
}
function vr(e, t) {
	let n = t.trim();
	if (n) return e === "int" ? /^-?\d+$/.test(n) ? Number(n) : void 0 : e === "float" ? Number.isNaN(Number(n)) ? void 0 : Number(n) : e === "bool" ? n === "true" || n !== "false" && void 0 : e === "duration" && Qn(n) === null ? void 0 : n;
}
function yr({ id: e, inner: t, value: n, onCommit: r, disabled: i }) {
	let a = M(), [o, s] = x(""), c = vr(t, o), l = () => {
		c !== void 0 && (r([...n, c]), s(""));
	};
	return /* @__PURE__ */ E("div", {
		className: "k-list-edit",
		children: [n.length > 0 && /* @__PURE__ */ T("div", {
			className: "k-chip-row",
			children: n.map((e, t) => /* @__PURE__ */ E("span", {
				className: "k-chip",
				children: [String(e), /* @__PURE__ */ T("button", {
					type: "button",
					"aria-label": a("ui.schemaForm.removeItem", { item: String(e) }),
					disabled: i,
					onClick: () => r(n.filter((e, n) => n !== t)),
					children: /* @__PURE__ */ T(te, { size: 12 })
				})]
			}, t))
		}), /* @__PURE__ */ E("div", {
			className: "k-input-group",
			children: [/* @__PURE__ */ T(R, {
				id: e,
				value: o,
				onChange: (e) => s(e.target.value),
				placeholder: a("ui.schemaForm.addItem", { type: t }),
				disabled: i,
				mono: !0,
				invalid: o.trim() !== "" && c === void 0,
				onKeyDown: (e) => {
					e.key === "Enter" && (e.preventDefault(), l());
				}
			}), /* @__PURE__ */ T(an, {
				label: a("common.add"),
				icon: /* @__PURE__ */ T(D, {}),
				variant: "secondary",
				onClick: l,
				disabled: i || c === void 0
			})]
		})]
	});
}
// src/ui/Menu.tsx
function br({ label: e, icon: t, items: n, align: r = "right", variant: i, size: a }) {
	let [o, s] = x(!1), [c, u] = x({}), d = b(null), f = b(null);
	return g(() => {
		if (!o) return;
		let e = (e) => {
			let t = e.target;
			!d.current?.contains(t) && !f.current?.contains(t) && s(!1);
		}, t = (e) => {
			e.key === "Escape" && s(!1);
		}, n = () => s(!1), r = performance.now(), i = (e) => {
			performance.now() - r < 300 || (e.target === window || e.target instanceof Node && d.current && e.target.contains(d.current)) && n();
		};
		return document.addEventListener("mousedown", e), document.addEventListener("keydown", t), window.addEventListener("resize", n), window.addEventListener(hn, n), window.addEventListener("scroll", i, !0), f.current?.querySelector(".k-menu-item")?.focus(), () => {
			document.removeEventListener("mousedown", e), document.removeEventListener("keydown", t), window.removeEventListener("resize", n), window.removeEventListener(hn, n), window.removeEventListener("scroll", i, !0);
		};
	}, [o]), g(() => {
		let e = d.current;
		if (!e) return;
		let t = (e) => {
			let { x: t, y: n } = e.detail, r = window.innerHeight - n - 16, i = n - 16, a = r < 240 && i > r, o = t > window.innerWidth - 260;
			u({
				position: "fixed",
				...o ? { right: Math.max(8, window.innerWidth - t) } : { left: t },
				maxHeight: Math.max(120, a ? i : r),
				overflowY: "auto",
				...a ? { bottom: window.innerHeight - n } : { top: n }
			}), s(!0);
		};
		return e.addEventListener("k-context", t), () => e.removeEventListener("k-context", t);
	}, []), /* @__PURE__ */ E("div", {
		ref: d,
		style: { position: "relative" },
		"data-k-menu": "",
		children: [/* @__PURE__ */ T(an, {
			label: e,
			icon: t,
			variant: i,
			size: a,
			onClick: () => {
				let e = d.current?.getBoundingClientRect();
				if (e) {
					let t = r === "right" ? { right: window.innerWidth - e.right } : { left: e.left }, n = window.innerHeight - e.bottom - 22, i = e.top - 22, a = n < 240 && i > n;
					u({
						position: "fixed",
						...t,
						maxHeight: Math.max(120, a ? i : n),
						overflowY: "auto",
						...a ? { bottom: window.innerHeight - e.top + 6 } : { top: e.bottom + 6 }
					});
				}
				s((e) => !e);
			},
			"aria-haspopup": "menu",
			"aria-expanded": o
		}), o && O(/* @__PURE__ */ T("div", {
			ref: f,
			className: "k-popover",
			role: "menu",
			style: c,
			children: n.map((e, t) => /* @__PURE__ */ E(l, { children: [e.sep && t > 0 && /* @__PURE__ */ T("div", {
				className: "k-menu-sep",
				role: "separator"
			}), /* @__PURE__ */ E("button", {
				type: "button",
				role: "menuitem",
				className: "k-menu-item" + (e.danger ? " danger" : ""),
				disabled: e.disabled,
				onClick: () => {
					s(!1), e.onSelect();
				},
				children: [e.icon, e.label]
			})] }, t))
		}), document.body)]
	});
}
// src/ui/format.ts
function xr(e) {
	if (e == null) return "-";
	if (e < 1024) return `${e} B`;
	let t = [
		"KB",
		"MB",
		"GB",
		"TB"
	], n = e / 1024, r = 0;
	for (; n >= 1024 && r < t.length - 1;) n /= 1024, r++;
	return `${n < 10 ? n.toFixed(1) : Math.round(n)} ${t[r]}`;
}
function Sr(e, t = Date.now()) {
	if (e == null || e === "") return "-";
	let n = typeof e == "number" ? e : Date.parse(e);
	if (Number.isNaN(n)) return String(e);
	let r = Math.round((t - n) / 1e3);
	return r < 0 ? Cr(n) : r < 45 ? A("format.ago.justNow") : r < 3600 ? A("format.ago.minutes", { n: Math.round(r / 60) }) : r < 86400 ? A("format.ago.hours", { n: Math.round(r / 3600) }) : r < 172800 ? A("format.ago.yesterday") : new Date(n).toLocaleDateString(Be());
}
function Cr(e) {
	if (e == null || e === "") return "-";
	let t = new Date(typeof e == "number" ? e : Date.parse(e));
	return Number.isNaN(t.getTime()) ? String(e) : t.toLocaleString(Be(), {
		dateStyle: "medium",
		timeStyle: "short"
	});
}
// src/admin/ui/index.tsx
var wr = (...e) => e.filter(Boolean).join(" "), G = (e, t) => Fn.ok(e, t), K = (e, t) => Fn.error(e, t), Tr = C(() => ({ req: null }));
function q(e) {
	return new Promise((t) => Tr.setState({ req: {
		...e,
		resolve: (e) => t(e !== !1)
	} }));
}
function Er(e) {
	return new Promise((t) => Tr.setState({ req: {
		...e,
		resolve: (e) => t(e === !1 ? null : e)
	} }));
}
function Dr() {
	let e = M(), t = Tr((e) => e.req), [n, r] = x("");
	g(() => r(t?.prompt?.initial ?? ""), [t]);
	let i = (e) => {
		t?.resolve(e), Tr.setState({ req: null });
	};
	if (t?.typed) return /* @__PURE__ */ T(Sn, {
		open: !0,
		title: t.title,
		message: t.body,
		word: t.typed,
		action: t.ok ?? e("admin.ui.confirm"),
		onClose: () => i(!1),
		onConfirm: () => i("ok")
	});
	let a = !!t && (!t.prompt || n.trim().length > 0), o = () => a && i(t?.prompt ? n.trim() : "ok");
	return /* @__PURE__ */ T(vn, {
		open: !!t,
		title: t?.title,
		onClose: () => i(!1),
		footer: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(L, {
			variant: "ghost",
			onClick: () => i(!1),
			children: e("common.cancel")
		}), /* @__PURE__ */ T(L, {
			variant: t?.danger ? "danger-fill" : "primary",
			disabled: !a,
			onClick: o,
			"data-autofocus": !t?.prompt || void 0,
			children: t?.ok ?? e("admin.ui.confirm")
		})] }),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [t?.body, t?.prompt ? /* @__PURE__ */ T(R, {
				"data-autofocus": !0,
				value: n,
				placeholder: t.prompt.placeholder,
				maxLength: t.prompt.maxLength,
				onChange: (e) => r(e.target.value),
				onKeyDown: (e) => {
					e.key === "Enter" && (e.preventDefault(), o());
				}
			}) : null]
		})
	});
}
function Or({ health: e, max: t, wide: n }) {
	let r = M();
	if (!t) return null;
	let i = Math.max(0, Math.min(100, Math.round((e ?? 0) / t * 100)));
	return /* @__PURE__ */ T("div", {
		className: wr("ka-hp", i < 25 && "crit", i >= 25 && i < 50 && "low", n && "wide"),
		title: r("admin.ui.healthTitle", {
			health: String(e),
			max: t
		}),
		role: "meter",
		"aria-valuenow": i,
		"aria-valuemin": 0,
		"aria-valuemax": 100,
		children: /* @__PURE__ */ T("i", { style: { width: `${i}%` } })
	});
}
function kr({ k: e, v: t, s: n }) {
	return /* @__PURE__ */ E("div", {
		className: "ka-stat",
		children: [
			/* @__PURE__ */ T("div", {
				className: "ka-stat-k",
				children: e
			}),
			/* @__PURE__ */ T("div", {
				className: "ka-stat-v",
				children: t
			}),
			n == null ? null : /* @__PURE__ */ T("div", {
				className: "ka-stat-s",
				children: n
			})
		]
	});
}
var Ar = ({ children: e }) => /* @__PURE__ */ T("div", {
	className: "ka-stats",
	children: e
}), jr = (e) => String(e).replace(/[^\p{L}\p{N}]/gu, "").slice(0, 2) || "?";
function Mr({ name: e, online: t }) {
	let n = M();
	return /* @__PURE__ */ E("span", {
		className: "ka-avatar",
		children: [jr(e), t ? /* @__PURE__ */ T("i", {
			className: "ka-online",
			title: n("admin.ui.online")
		}) : null]
	});
}
function Nr({ value: e, onChange: t, suggestions: n, onPick: r, placeholder: i, onEnter: a, onTab: o, onHistory: s, autoFocus: c, className: l, mono: u, id: d, "aria-label": f }) {
	let [p, m] = x(-1), [h, _] = x(!1);
	g(() => {
		m(-1), _(n.length > 0);
	}, [n]);
	let v = (e) => {
		_(!1), r(e);
	};
	return /* @__PURE__ */ E("div", {
		className: wr("ka-suggest-host", l),
		children: [/* @__PURE__ */ T(R, {
			id: d,
			"aria-label": f,
			mono: u,
			value: e,
			placeholder: i,
			autoFocus: c,
			autoComplete: "off",
			spellCheck: !1,
			role: "combobox",
			"aria-expanded": h && n.length > 0,
			onChange: (e) => t(e.target.value),
			onKeyDown: (e) => {
				let t = n.length, r = h && t > 0;
				r && (e.key === "ArrowDown" || e.key === "ArrowUp") ? (e.preventDefault(), m((n) => (n + (e.key === "ArrowDown" ? 1 : -1) + t) % t)) : !r && s && (e.key === "ArrowDown" || e.key === "ArrowUp") ? (e.preventDefault(), s(e.key === "ArrowUp" ? -1 : 1)) : r && (e.key === "Tab" || e.key === "Enter" && p >= 0) ? (e.preventDefault(), v(n[Math.max(0, p)])) : e.key === "Escape" && r ? (e.preventDefault(), _(!1)) : e.key === "Tab" && o ? (e.preventDefault(), o()) : e.key === "Enter" && a && (e.preventDefault(), _(!1), a());
			},
			onBlur: () => setTimeout(() => _(!1), 120),
			onFocus: () => _(n.length > 0)
		}), h && n.length ? /* @__PURE__ */ T("div", {
			className: "ka-suggest",
			role: "listbox",
			children: n.map((e, t) => /* @__PURE__ */ E("button", {
				type: "button",
				role: "option",
				"aria-selected": t === p,
				className: t === p ? "sel" : "",
				onMouseDown: (t) => {
					t.preventDefault(), v(e);
				},
				children: [/* @__PURE__ */ T("span", { children: e.label }), e.sub ? /* @__PURE__ */ T("small", { children: e.sub }) : null]
			}, e.key))
		}) : null]
	});
}
function Pr(e) {
	if (e = Math.max(0, Math.round(e)), e < 60) return A("admin.ui.durS", { s: e });
	let t = Math.floor(e / 60), n = Math.floor(t / 60), r = Math.floor(n / 24);
	return r ? A("admin.ui.durDH", {
		d: r,
		h: n % 24
	}) : n ? A("admin.ui.durHM", {
		h: n,
		m: t % 60
	}) : A("admin.ui.durM", { m: t });
}
var Fr = (e) => e ? Ir(e * 1e3) : "-", Ir = (e) => {
	let t = Math.round((Date.now() - e) / 1e3);
	return t < 45 ? A("admin.ui.justNow") : t < 172800 ? A("admin.ui.ago", { dur: Pr(t) }) : new Date(e).toLocaleDateString(Be(), {
		month: "short",
		day: "numeric"
	});
}, Lr = (e) => e ? (/* @__PURE__ */ new Date(e * 1e3)).toLocaleString(Be(), {
	month: "short",
	day: "numeric",
	hour: "2-digit",
	minute: "2-digit"
}) : "-", Rr = (e) => e ? `${Math.round(e.x)}, ${Math.round(e.y)}, ${Math.round(e.z)}` : "-";
function zr(e, t) {
	let [n, r] = x(e);
	return g(() => {
		let n = setTimeout(() => r(e), t);
		return () => clearTimeout(n);
	}, [e, t]), n;
}
// src/admin/common.tsx
function Br(e) {
	return function(t) {
		let n = Nt(), { id: r } = a();
		return n || !r ? /* @__PURE__ */ T(e, { ...t }) : /* @__PURE__ */ T(jt, {
			serverId: r,
			children: /* @__PURE__ */ T(e, { ...t })
		});
	};
}
function Vr({ ops: e, children: t, what: n }) {
	let r = M(), i = Ft(), a = (Array.isArray(e) ? e : [e]).filter((e) => !i.ops.has(e));
	return i.session ? a.length ? /* @__PURE__ */ T(H, {
		tone: "ember",
		title: n ? r("admin.common.whatNeedsNewer", { what: n }) : r("admin.common.needsNewer"),
		children: r("admin.common.noOpYet", { ops: a.map((e) => `'${e}'`).join(", ") })
	}) : /* @__PURE__ */ T(w, { children: t }) : null;
}
function Hr() {
	return Ft().state?.players ?? [];
}
var J = (e) => e?.name || (e?.entity == null ? A("admin.common.someone") : A("admin.common.playerEntity", { entity: e.entity }));
function Ur({ value: e, onChange: t, players: n, placeholder: r, exclude: i, id: a }) {
	let o = M(), s = n.filter((e) => e.entity !== i).map((e) => ({
		value: String(e.entity),
		label: J(e)
	}));
	return /* @__PURE__ */ T(z, {
		id: a,
		value: e == null ? "" : String(e),
		onChange: (e) => t(e ? Number(e) : null),
		options: [{
			value: "",
			label: s.length ? r ?? o("admin.common.pickPlayer") : o("admin.common.nobodyOnline")
		}, ...s]
	});
}
function Wr(e) {
	switch (e) {
		case "Common": return A("admin.players.rarities.common");
		case "Uncommon": return A("admin.players.rarities.uncommon");
		case "Rare": return A("admin.players.rarities.rare");
		case "Epic": return A("admin.players.rarities.epic");
		case "Legendary": return A("admin.players.rarities.legendary");
		default: return e;
	}
}
var Gr = (e) => typeof e == "string" && e.length > 0 ? e : null;
function Kr(e, t) {
	let n = e.definition ?? {}, r = Gr(e.displayName) ?? Gr(n.texts?.en?.name) ?? Gr(n.texts?.["*"]?.name);
	if (r || e.kind !== "recipe") return r;
	let i = Gr(n.outputs?.[0]?.item);
	return i ? t?.get(i) ?? null : null;
}
function qr() {
	let e = I("content_definitions", {}, {
		enabled: P("content_definitions"),
		staleTime: 3e4
	});
	return y(() => {
		let t = /* @__PURE__ */ new Map();
		for (let n of e.data ?? []) {
			let e = n.kind === "item" ? Kr(n) : null;
			e && t.set(n.name, e);
		}
		return t;
	}, [e.data]);
}
function Jr(e, t) {
	let n = e.debug ?? (t.has(e.name) ? e.name : null), r = (n ? t.get(n) : void 0) ?? e.name;
	return {
		name: r,
		debug: n && n !== r ? n : null
	};
}
function Yr({ value: e, onPick: t, placeholder: n, id: r }) {
	let i = M(), [a, o] = x(e?.name ?? ""), s = zr(a.trim(), 180), c = P("item_search"), l = I("item_search", { q: s }, {
		enabled: c && s.length >= 2 && s !== e?.name,
		staleTime: 6e4
	}), u = qr(), d = y(() => (l.data ?? []).map((e) => {
		let t = Jr(e, u);
		return {
			key: String(e.id),
			label: t.name,
			sub: /* @__PURE__ */ E("span", {
				className: "k-mono",
				children: [
					t.debug ? `${t.debug} · ` : "",
					"#",
					e.id,
					e.gear ? i("admin.common.gearTag") : ""
				]
			}),
			value: {
				...e,
				name: t.name
			}
		};
	}), [
		l.data,
		u,
		i
	]);
	return /* @__PURE__ */ T(Nr, {
		id: r,
		value: a,
		placeholder: c ? n ?? i("admin.common.searchItems") : i("admin.common.noItemSearch"),
		onChange: (e) => {
			o(e);
			let n = /^#?(\d{3,})$/.exec(e.trim());
			t(n ? {
				id: Number(n[1]),
				name: `#${n[1]}`,
				gear: !1
			} : null);
		},
		suggestions: s === e?.name ? [] : d,
		onPick: (e) => {
			o(e.value.name), t(e.value);
		}
	});
}
var Xr = (e) => e ? {
	x: String(Zr(e.x)),
	y: String(Zr(e.y)),
	z: String(Zr(e.z))
} : {
	x: "",
	y: "",
	z: ""
}, Zr = (e) => Math.round(e * 10) / 10;
function Qr(e) {
	let t = {
		x: Number(e.x),
		y: Number(e.y),
		z: Number(e.z)
	};
	return [
		e.x,
		e.y,
		e.z
	].some((e) => e.trim() === "") || ![
		t.x,
		t.y,
		t.z
	].every(Number.isFinite) ? {
		pos: null,
		error: A("admin.common.enterXyz")
	} : [
		t.x,
		t.y,
		t.z
	].every((e) => e > -1e6 && e < 1e6) ? {
		pos: t,
		error: null
	} : {
		pos: null,
		error: A("admin.common.coordsRange")
	};
}
function $r({ value: e, onChange: t, idPrefix: n }) {
	let r = M();
	return /* @__PURE__ */ T("div", {
		className: "ka-coords",
		children: [
			"x",
			"y",
			"z"
		].map((i) => /* @__PURE__ */ T(R, {
			id: n ? `${n}-${i}` : void 0,
			inputMode: "decimal",
			placeholder: i === "y" ? r("admin.common.yHeight") : i,
			"aria-label": i,
			value: e[i],
			onChange: (n) => t({
				...e,
				[i]: n.target.value
			}),
			className: "ka-coord"
		}, i))
	});
}
var ei = /^\d{17}$/, ti = /^(\*|[a-z0-9_:-]+(\.[a-z0-9_:-]+)*(\.\*)?)$/, ni = /^[A-Za-z0-9_:-]{1,40}$/, ri = /^(?!\.)[A-Za-z0-9._-]{1,64}$/, ii = /^(\[\d{1,4}\]\.)?[A-Za-z_][A-Za-z0-9_]*(\[\d{1,4}\])?(\.[A-Za-z_][A-Za-z0-9_]*(\[\d{1,4}\])?){0,8}$/;
function ai(e, t = !1) {
	let n = e.trim();
	return !ei.test(n) || !n.startsWith("765611") || t && BigInt(n) <= 76561197960265728n ? A("admin.common.steamIdFormat") : null;
}
function oi(e) {
	let t = e.trim().toLowerCase();
	return t.startsWith("-") ? A("admin.common.nodeNoMinus") : !t || t.length > 128 || !ti.test(t) ? A("admin.common.nodeFormat") : null;
}
var Y = () => Mt(), si = {
	auto: "admin.connection.transportAuto",
	tcp: "admin.connection.transportTcp",
	file: "admin.connection.transportFile",
	ssh: "admin.connection.transportSsh"
}, ci = () => (Ze()?.kinds ?? []).map((e) => ({
	value: e,
	label: A(si[e])
}));
function li() {
	let e = M(), t = Ft();
	if (t.session) return null;
	if (t.status === "connecting" || t.status === "idle") return /* @__PURE__ */ T("span", {
		className: "k-empty-title",
		children: e("admin.connection.connecting")
	});
	let n = ci();
	return /* @__PURE__ */ T(H, {
		tone: "warn",
		title: e("admin.connection.notConnected"),
		actions: /* @__PURE__ */ E(w, { children: [n.length ? /* @__PURE__ */ T(z, {
			"aria-label": e("admin.connection.transport"),
			value: t.want,
			onChange: (e) => Et(t.serverId, e),
			options: n,
			style: {
				width: "auto",
				height: 32
			}
		}) : null, /* @__PURE__ */ T(L, {
			size: "s",
			onClick: () => Et(t.serverId),
			children: e("admin.connection.reconnect")
		})] }),
		children: e("admin.connection.offline", { reason: t.error?.message ?? e("admin.connection.notAnswering") })
	});
}
// src/admin/pages/AdminPlayersPage.tsx
var ui = [
	"",
	"Common",
	"Uncommon",
	"Rare",
	"Epic",
	"Legendary"
];
function di() {
	let e = M(), t = Ft(), n = Jt(Y()), r = t.state?.players ?? [], i = nn(n, I("permissions_get", {}, { staleTime: 3e4 }).data?.players ?? {}), a = new Set(r.map((e) => e.steamId)), [o, s] = x("world"), [c, l] = x(null), u = r.find((e) => e.entity === c) ?? r[0] ?? null;
	return /* @__PURE__ */ E(Bn, {
		fill: !0,
		children: [
			/* @__PURE__ */ T(zn, {
				title: e("admin.players.title"),
				sub: e("admin.players.sub")
			}),
			/* @__PURE__ */ T(li, {}),
			t.session ? /* @__PURE__ */ T(Vr, {
				ops: "players",
				what: e("admin.players.needsWhat"),
				children: /* @__PURE__ */ E(Vn, {
					form: !0,
					aside: u && o === "world" ? /* @__PURE__ */ T(fi, {
						p: u,
						players: r
					}, u.entity) : /* @__PURE__ */ T("span", {
						className: "k-empty-title",
						children: e(o === "world" ? "admin.players.nobodyAside" : "admin.players.pickInWorld")
					}),
					children: [/* @__PURE__ */ T(Wn, {
						"aria-label": e("admin.players.title"),
						value: o,
						onChange: s,
						options: [{
							id: "world",
							label: e("admin.players.tabWorld", { count: r.length })
						}, {
							id: "seen",
							label: e("admin.players.tabSeen", { count: i.length })
						}]
					}), o === "world" ? /* @__PURE__ */ T(Gn, {
						"aria-label": e("admin.players.worldTable"),
						fill: !0,
						rows: r,
						rowKey: (e) => String(e.entity),
						selectedKey: u ? String(u.entity) : null,
						onRowClick: (e) => l(e.entity),
						columns: [
							{
								key: "name",
								header: e("admin.players.colPlayer"),
								sort: (e, t) => J(e).localeCompare(J(t)),
								render: (t) => /* @__PURE__ */ T(Kn, {
									name: J(t),
									id: t.steamId ?? e("admin.players.entityId", { entity: t.entity })
								})
							},
							{
								key: "state",
								header: e("admin.players.colState"),
								width: "96px",
								render: (t) => t.healthMax && (t.health ?? 0) <= 0 ? /* @__PURE__ */ T("span", {
									className: "k-badge k-tone-err",
									children: e("admin.players.stateDead")
								}) : t.god ? /* @__PURE__ */ T("span", {
									className: "k-badge k-tone-ember",
									children: e("admin.players.godMode")
								}) : t.admin ? /* @__PURE__ */ T("span", {
									className: "k-badge k-tone-warn",
									children: e("admin.players.stateAdmin")
								}) : /* @__PURE__ */ T("span", {
									className: "k-badge k-tone-ok",
									children: e("admin.players.statePlaying")
								})
							},
							{
								key: "hp",
								header: e("admin.players.colHealth"),
								width: "130px",
								render: (e) => e.healthMax ? /* @__PURE__ */ T(Or, {
									health: e.health,
									max: e.healthMax
								}) : /* @__PURE__ */ T("span", {
									className: "k-muted",
									children: "-"
								})
							},
							{
								key: "pos",
								header: e("admin.players.colPosition"),
								width: "140px",
								align: "right",
								render: (e) => /* @__PURE__ */ T("span", {
									className: "k-mono",
									children: Rr(e.pos)
								})
							}
						],
						empty: e("admin.players.nobody")
					}) : /* @__PURE__ */ T(Gn, {
						"aria-label": e("admin.players.knownTable"),
						fill: !0,
						rows: i,
						rowKey: (e) => e.steamId,
						columns: [
							{
								key: "name",
								header: e("admin.players.colPlayer"),
								render: (t) => /* @__PURE__ */ T(Kn, {
									name: t.name || e("admin.players.unknown"),
									id: t.steamId
								}),
								sort: (e, t) => e.name.localeCompare(t.name)
							},
							{
								key: "seen",
								header: e("admin.players.colLastSeen"),
								width: "130px",
								render: (t) => a.has(t.steamId) ? /* @__PURE__ */ T("span", {
									className: "k-badge k-tone-ok",
									children: e("admin.players.online")
								}) : /* @__PURE__ */ T("span", {
									className: "k-muted",
									children: t.lastSeen ? Fr(t.lastSeen) : e("admin.players.inPermissions")
								}),
								sort: (e, t) => e.lastSeen - t.lastSeen
							},
							{
								key: "first",
								header: e("admin.players.colFirstSeen"),
								width: "120px",
								render: (e) => /* @__PURE__ */ T("span", {
									className: "k-muted",
									children: e.firstSeen ? Fr(e.firstSeen) : "-"
								})
							}
						],
						empty: e("admin.players.knownEmpty")
					})]
				})
			}) : null
		]
	});
}
function fi({ p: e, players: t }) {
	let n = M(), r = Y(), i = J(e), a = F("heal"), o = F("god"), s = F("kill"), c = P("heal"), [l, u] = x(!1), d = async () => {
		await q({
			title: n("admin.players.killTitle", { name: i }),
			body: /* @__PURE__ */ T("p", { children: n("admin.players.killBody", { name: i }) }),
			typed: i,
			ok: n("admin.players.kill")
		}) && s.mutate({ entity: e.entity }, {
			onSuccess: (e) => e.killed ? G(n("admin.players.killed", { name: i })) : K({ message: `${i}: ${e.state}` }),
			onError: (e) => K(e)
		});
	}, f = async () => {
		let t = await Er({
			title: n("admin.players.saveSpot"),
			ok: n("common.save"),
			prompt: {
				placeholder: n("admin.players.waypointPlaceholder"),
				maxLength: 40,
				initial: n("admin.players.spotName", { name: i }).slice(0, 40)
			}
		});
		if (t && e.pos) {
			let i = $t(r, t, e.pos);
			i ? K({ message: i }) : G(n("admin.players.savedWaypoint", { name: t }));
		}
	}, p = /* @__PURE__ */ T("span", {
		className: "k-mono",
		children: Rr(e.pos)
	}), m = e.permissions || n("admin.players.rightsNone");
	return /* @__PURE__ */ E(w, { children: [
		/* @__PURE__ */ E("div", { children: [
			/* @__PURE__ */ T("div", {
				style: {
					fontSize: 20,
					fontWeight: 500,
					color: "var(--k-text-0)"
				},
				children: i
			}),
			/* @__PURE__ */ T("div", {
				className: "k-mono k-muted",
				style: { fontSize: 14.5 },
				children: n("admin.players.idLine", {
					id: e.steamId ?? n("admin.players.noSteamId"),
					entity: e.entity
				})
			}),
			/* @__PURE__ */ T("div", {
				className: "k-hint",
				children: e.healthMax ? j("admin.players.whereHealth", {
					pos: p,
					health: /* @__PURE__ */ E("span", {
						className: "k-mono",
						children: [
							e.health,
							" / ",
							e.healthMax
						]
					}),
					rights: m
				}) : j("admin.players.where", {
					pos: p,
					rights: m
				})
			})
		] }),
		/* @__PURE__ */ T(pi, {
			p: e,
			players: t
		}),
		/* @__PURE__ */ T(mi, { p: e }),
		/* @__PURE__ */ E("div", {
			className: "ka-tool",
			children: [
				/* @__PURE__ */ T("span", {
					className: "k-label",
					children: n("admin.players.combat")
				}),
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [/* @__PURE__ */ T(L, {
						size: "s",
						disabled: !c,
						loading: a.isPending,
						onClick: () => a.mutate({ entity: e.entity }, {
							onSuccess: (e) => G(n("admin.players.healed", { name: i }), `+${e.healed ?? 0}`),
							onError: (e) => K(e)
						}),
						children: n("admin.players.healFull")
					}), /* @__PURE__ */ T(L, {
						size: "s",
						variant: "danger",
						disabled: !c,
						loading: s.isPending,
						onClick: d,
						children: n("admin.players.kill")
					})]
				}),
				/* @__PURE__ */ T(ln, {
					checked: !!e.god,
					disabled: !c || o.isPending,
					label: n("admin.players.godMode"),
					onChange: (t) => o.mutate({
						entity: e.entity,
						on: t
					}, {
						onSuccess: (e) => G(e.god ? n("admin.players.godOn", { name: i }) : n("admin.players.godOff", { name: i })),
						onError: (e) => K(e)
					})
				})
			]
		}),
		/* @__PURE__ */ T(hi, { p: e }),
		/* @__PURE__ */ T(gi, { p: e }),
		/* @__PURE__ */ E("div", {
			className: "ka-inline",
			children: [/* @__PURE__ */ T("button", {
				type: "button",
				className: "k-link",
				onClick: () => u(!0),
				children: n("admin.players.inventory")
			}), /* @__PURE__ */ T("button", {
				type: "button",
				className: "k-link",
				disabled: !e.pos,
				onClick: f,
				children: n("admin.players.saveSpot")
			})]
		}),
		/* @__PURE__ */ T(vn, {
			open: l,
			onClose: () => u(!1),
			title: n("admin.players.inventoryOf", { name: i }),
			width: 640,
			footer: /* @__PURE__ */ T(L, {
				onClick: () => u(!1),
				children: n("common.close")
			}),
			children: l && /* @__PURE__ */ T(_i, { entity: e.entity })
		})
	] });
}
function pi({ p: e, players: t }) {
	let n = M(), r = Jt(Y()), [i, a] = x(""), [o, s] = x(Xr(null)), [c, l] = x(!0), u = F("teleport"), d = F("teleport_to_player"), f = P("teleport"), p = t.filter((t) => t.entity !== e.entity), m = [
		{
			value: "",
			label: n("admin.players.teleportTo")
		},
		...p.map((e) => ({
			value: `player:${e.entity}`,
			label: n("admin.players.tpPlayer", { name: J(e) })
		})),
		...r.waypoints.map((e) => ({
			value: `waypoint:${e.name}`,
			label: n("admin.players.tpWaypoint", { name: e.name })
		})),
		...r.graves.slice(-15).reverse().map((e) => ({
			value: `grave:${e.key}`,
			label: n("admin.players.tpGrave", {
				name: e.name ?? n("admin.common.someone"),
				ago: Fr(e.at)
			})
		})),
		{
			value: "coords",
			label: n("admin.players.tpCoords")
		}
	];
	return /* @__PURE__ */ E("div", {
		className: "ka-tool",
		children: [
			/* @__PURE__ */ T("label", {
				className: "k-label",
				htmlFor: `tp-${e.entity}`,
				children: n("admin.players.teleport")
			}),
			/* @__PURE__ */ E("div", {
				className: "ka-inline",
				style: { flexWrap: "nowrap" },
				children: [/* @__PURE__ */ T(z, {
					id: `tp-${e.entity}`,
					value: i,
					onChange: a,
					options: m
				}), /* @__PURE__ */ T(L, {
					size: "s",
					disabled: !f || !i,
					loading: u.isPending || d.isPending,
					onClick: () => {
						let t = {
							onSuccess: () => G(n("admin.players.teleported", { name: J(e) })),
							onError: (e) => K(e)
						};
						if (!i) return K({ message: n("admin.players.pickTarget") });
						if (i.startsWith("player:")) return d ? d.mutate({
							entity: e.entity,
							target: Number(i.slice(7))
						}, t) : void 0;
						let a = null;
						if (i === "coords") {
							let e = Qr(o);
							if (e.error) return K({ message: e.error });
							a = e.pos;
						} else if (i.startsWith("waypoint:")) a = r.waypoints.find((e) => e.name === i.slice(9))?.pos ?? null;
						else if (i.startsWith("grave:")) {
							let e = r.graves.find((e) => e.key === i.slice(6));
							a = e ? {
								...e.pos,
								y: e.pos.y + 1
							} : null;
						}
						if (!a) return K({ message: n("admin.players.unknownTarget") });
						u.mutate({
							entity: e.entity,
							x: a.x,
							y: a.y,
							z: a.z,
							safe: c
						}, t);
					},
					children: n("admin.players.go")
				})]
			}),
			i === "coords" ? /* @__PURE__ */ T($r, {
				value: o,
				onChange: s
			}) : null,
			i && !i.startsWith("player:") ? /* @__PURE__ */ T(ln, {
				checked: c,
				onChange: l,
				label: n("admin.players.findGround")
			}) : null
		]
	});
}
function mi({ p: e }) {
	let t = M(), [n, r] = x(null), [i, a] = x(1), [o, s] = x(null), [c, l] = x(""), u = F("give_item"), d = P("give_item");
	return /* @__PURE__ */ E("div", {
		className: "ka-tool",
		children: [
			/* @__PURE__ */ T("label", {
				className: "k-label",
				htmlFor: `give-${e.entity}`,
				children: t("admin.players.giveItem")
			}),
			/* @__PURE__ */ T(Yr, {
				id: `give-${e.entity}`,
				value: n,
				onPick: r,
				placeholder: t("admin.players.searchItems")
			}),
			/* @__PURE__ */ E("div", {
				className: "ka-inline",
				style: { flexWrap: "nowrap" },
				children: [
					/* @__PURE__ */ T(B, {
						"aria-label": t("admin.players.amount"),
						value: i,
						integer: !0,
						min: 1,
						max: 1e4,
						onCommit: a
					}),
					n?.gear ? /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(B, {
						"aria-label": t("admin.players.level"),
						placeholder: t("admin.players.levelPlaceholder"),
						value: o,
						integer: !0,
						min: 1,
						max: 100,
						onCommit: s
					}), /* @__PURE__ */ T(z, {
						"aria-label": t("admin.players.rarity"),
						value: c,
						onChange: l,
						options: ui.map((e) => ({
							value: e,
							label: e ? Wr(e) : t("admin.players.rarity")
						}))
					})] }) : null,
					/* @__PURE__ */ T(L, {
						size: "s",
						disabled: !d || !n,
						loading: u.isPending,
						onClick: () => {
							if (!n) return K({ message: t("admin.players.pickItem") });
							if (!Number.isInteger(i) || i < 1 || i > 1e4) return K({ message: t("admin.players.amountRange") });
							if (o != null && (!Number.isInteger(o) || o < 1 || o > 100)) return K({ message: t("admin.players.levelRange") });
							let r = {
								entity: e.entity,
								item: n.id,
								count: i
							};
							n.gear && o != null && (r.level = o), n.gear && c && (r.rarity = c), u.mutate(r, {
								onSuccess: (e) => G(e.given < e.requested ? t("admin.players.gavePartial", {
									given: e.given,
									item: n.name
								}) : t("admin.players.gave", {
									given: e.given,
									item: n.name
								})),
								onError: (e) => K(e)
							});
						},
						children: t("admin.players.give")
					})
				]
			})
		]
	});
}
function hi({ p: e }) {
	let t = M(), n = J(e), r = P("players_progression"), i = I("players_progression", { entity: e.entity }, { refetchInterval: 5e3 }), a = { invalidates: ["players_progression"] }, o = F("players_progression_set", a), s = F("give_xp", a), c = F("give_skill_points", a), l = F("skill_respec", a), [u, d] = x(null), [f, p] = x(1e3), [m, h] = x(1);
	if (!r) return null;
	let g = i.data?.progression, _ = (e) => ({
		onSuccess: (t) => t.ok ? G(e, t.reason || void 0) : K({ message: t.reason || t.status }),
		onError: (e) => K(e)
	}), v = () => {
		if (u == null || !Number.isInteger(u) || u < 1 || u > (g?.levelCap || 1e3)) return K({ message: g?.levelCap ? t("admin.players.levelToCap", { cap: g.levelCap }) : t("admin.players.levelToTheCap") });
		o.mutate({
			entity: e.entity,
			level: u
		}, {
			onSuccess: (e) => {
				let r = e.steps[0];
				r?.ok ? G(t("admin.players.isLevel", {
					name: n,
					level: r.after?.level ?? u
				}), r.reason || void 0) : K({ message: r?.reason || "refused" });
			},
			onError: (e) => K(e)
		});
	}, y = async () => {
		await q({
			title: t("admin.players.respecTitle", { name: n }),
			body: /* @__PURE__ */ T("p", { children: t("admin.players.respecBody", { name: n }) }),
			ok: t("admin.players.respec")
		}) && l.mutate({ entity: e.entity }, _(t("admin.players.respecDone", { name: n })));
	}, b = o.isPending || s.isPending || c.isPending || l.isPending;
	return /* @__PURE__ */ E("div", {
		className: "ka-tool",
		children: [
			/* @__PURE__ */ T("span", {
				className: "k-label",
				children: t("admin.players.progression")
			}),
			g?.hasLevel ? /* @__PURE__ */ E("span", {
				className: "k-hint",
				children: [
					/* @__PURE__ */ T("span", {
						className: "k-tone-info",
						children: t("admin.players.levelOf", {
							level: g.level,
							cap: g.levelCap
						})
					}),
					g.atLevelCap ? t("admin.players.atCap") : t("admin.players.xpOf", {
						xp: g.experience,
						needed: g.experienceNeeded
					}),
					g.experiencePending ? t("admin.players.xpPending", { pending: g.experiencePending }) : "",
					g.hasPoints ? t("admin.players.pointsUnspent", {
						points: g.skillPoints,
						total: g.skillPointsTotal
					}) : ".",
					g.overBudget ? /* @__PURE__ */ T("span", {
						className: "k-tone-err",
						children: t("admin.players.overBudget")
					}) : null
				]
			}) : /* @__PURE__ */ T("span", {
				className: "k-hint",
				children: i.error ? i.error.message : i.data ? t("admin.players.noProgression") : t("admin.players.reading")
			}),
			/* @__PURE__ */ E("div", {
				className: "ka-grid3",
				children: [
					/* @__PURE__ */ T(B, {
						"aria-label": t("admin.players.level"),
						placeholder: t("admin.players.levelPlaceholder"),
						value: u,
						integer: !0,
						min: 1,
						max: g?.levelCap || 1e3,
						onCommit: d
					}),
					/* @__PURE__ */ T(L, {
						size: "s",
						disabled: b || !g?.hasLevel,
						loading: o.isPending,
						onClick: v,
						children: t("admin.players.setLevel")
					}),
					/* @__PURE__ */ T(B, {
						"aria-label": t("admin.players.experience"),
						value: f,
						integer: !0,
						min: 1,
						max: 1e8,
						onCommit: p
					}),
					/* @__PURE__ */ T(L, {
						size: "s",
						disabled: b || !g?.hasLevel || g.atLevelCap,
						loading: s.isPending,
						onClick: () => s.mutate({
							entity: e.entity,
							amount: f
						}, _(t("admin.players.gaveXp", {
							xp: f,
							name: n
						}))),
						children: t("admin.players.giveXp")
					}),
					/* @__PURE__ */ T(B, {
						"aria-label": t("admin.players.skillPoints"),
						value: m,
						integer: !0,
						min: 1,
						max: 1e3,
						onCommit: h
					}),
					/* @__PURE__ */ T(L, {
						size: "s",
						disabled: b || !g?.hasPoints || !g.givableSkillPoints,
						loading: c.isPending,
						title: g ? t("admin.players.givePointsTip", { count: g.givableSkillPoints }) : void 0,
						onClick: () => c.mutate({
							entity: e.entity,
							amount: m
						}, _(t("admin.players.gavePoints", {
							points: m,
							name: n
						}))),
						children: t("admin.players.givePoints")
					})
				]
			}),
			/* @__PURE__ */ T("div", { children: /* @__PURE__ */ T("button", {
				type: "button",
				className: "k-link",
				disabled: b || !g?.hasSkills,
				onClick: y,
				children: t("admin.players.respecTree")
			}) })
		]
	});
}
function gi({ p: e }) {
	let t = M(), [n, r] = x(""), i = F("tell");
	if (!P("tell")) return null;
	let a = () => {
		let a = n.trim();
		if (!a || a.length > 500) return K({ message: t("admin.players.messageRange") });
		i.mutate({
			entity: e.entity,
			text: a
		}, {
			onSuccess: () => {
				G(t("admin.players.told", { name: J(e) })), r("");
			},
			onError: (e) => K(e)
		});
	};
	return /* @__PURE__ */ E("div", {
		className: "ka-tool",
		children: [/* @__PURE__ */ T("label", {
			className: "k-label",
			htmlFor: `tell-${e.entity}`,
			children: t("admin.players.message")
		}), /* @__PURE__ */ E("div", {
			className: "ka-inline",
			style: { flexWrap: "nowrap" },
			children: [/* @__PURE__ */ T(R, {
				id: `tell-${e.entity}`,
				value: n,
				maxLength: 500,
				placeholder: t("admin.players.onlySees", { name: J(e) }),
				onChange: (e) => r(e.target.value),
				onKeyDown: (e) => e.key === "Enter" && a()
			}), /* @__PURE__ */ T(L, {
				size: "s",
				loading: i.isPending,
				onClick: a,
				children: t("admin.players.send")
			})]
		})]
	});
}
function _i({ entity: e }) {
	let t = M(), n = I("inventory", { entity: e }, { refetchInterval: 1e4 });
	if (n.error) return /* @__PURE__ */ T("div", {
		className: "k-error-text",
		children: n.error.message
	});
	if (!n.data) return /* @__PURE__ */ T("div", {
		className: "k-muted",
		children: t("admin.players.readingInventory")
	});
	let r = n.data.inventories ?? [];
	return r.length ? /* @__PURE__ */ T("div", {
		className: "k-stack",
		style: { gap: 16 },
		children: r.map((e) => /* @__PURE__ */ T(U, {
			title: e.category,
			sub: t("admin.players.slotsUsed", { slots: e.slots.length }),
			children: /* @__PURE__ */ T("div", {
				className: "ka-list",
				children: e.slots.map((e, n) => /* @__PURE__ */ E("div", {
					className: "ka-row-item",
					children: [
						/* @__PURE__ */ T("span", { children: e.name ?? `#${e.item}` }),
						e.level ? /* @__PURE__ */ T(V, {
							tone: "info",
							children: t("admin.players.levelBadge", { level: e.level })
						}) : null,
						e.rarity ? /* @__PURE__ */ T(V, {
							tone: "ember",
							children: e.rarity
						}) : null,
						e.equipped ? /* @__PURE__ */ T(V, {
							tone: "ok",
							children: t("admin.players.equipped")
						}) : null,
						/* @__PURE__ */ T("span", { className: "k-spacer" }),
						/* @__PURE__ */ E("span", {
							className: "k-muted",
							children: ["× ", e.count]
						})
					]
				}, n))
			})
		}, e.link))
	}) : /* @__PURE__ */ T("div", {
		className: "k-muted",
		children: t("admin.players.noInventories")
	});
}
var vi = Br(di);
// src/admin/catalog.ts
function yi() {
	let e = Qe().assetsRoot?.();
	return e ? e.endsWith("/") ? e : `${e}/` : "/assets-local/";
}
function bi() {
	return s({
		queryKey: ["assets-local", "creature-templates"],
		staleTime: Infinity,
		retry: !1,
		queryFn: async () => {
			let e = await fetch(`${yi()}data/creature-templates.json`);
			if (!e.ok || !(e.headers.get("content-type") ?? "").includes("json")) return [];
			let t = await e.json();
			return Object.entries(t).filter(([, e]) => e.name).map(([e, t]) => ({
				guid: e,
				template: t.template,
				name: t.name
			})).sort((e, t) => e.name.localeCompare(t.name));
		}
	});
}
var xi = (e) => e ? e.replace(/^(Base_)?(Enemy|Animal)_(Wildlife_|Wildbeast_)?/, "").replace(/_(T\d|AG2|Dungeon|Patrol)$/g, "").replace(/_/g, " ") : A("admin.catalog.creature"), Si = [
	"build",
	"break",
	"terraform",
	"interact",
	"loot",
	"craft",
	"spawn"
], Ci = [
	"build",
	"break",
	"terraform",
	"interact",
	"loot"
];
function wi(e) {
	switch (e) {
		case "build": return A("admin.zones.action.build");
		case "break": return A("admin.zones.action.break");
		case "terraform": return A("admin.zones.action.terraform");
		case "interact": return A("admin.zones.action.interact");
		case "loot": return A("admin.zones.action.loot");
		case "craft": return A("admin.zones.action.craft");
		case "spawn": return A("admin.zones.action.spawn");
		default: return e;
	}
}
function Ti(e) {
	switch (e) {
		case "build": return A("admin.zones.actionHint.build");
		case "break": return A("admin.zones.actionHint.break");
		case "terraform": return A("admin.zones.actionHint.terraform");
		case "interact": return A("admin.zones.actionHint.interact");
		case "loot": return A("admin.zones.actionHint.loot");
		case "craft": return A("admin.zones.actionHint.craft");
		case "spawn": return A("admin.zones.actionHint.spawn");
		default: return;
	}
}
var Ei = () => ({
	name: "",
	x: "",
	y: "",
	z: "",
	radius: "30",
	deny: [...Ci],
	owners: [],
	allowAdmins: !0,
	editing: !1
});
function Di(e) {
	let t = e.name.trim();
	if (!t || t.length > 64) return { error: A("admin.zones.nameRange") };
	let n = (e) => e.trim() === "" ? NaN : Number(e), r = {
		x: n(e.x),
		y: n(e.y),
		z: n(e.z)
	};
	if (![
		r.x,
		r.y,
		r.z
	].every(Number.isFinite)) return { error: A("admin.zones.needCentre") };
	if (![
		r.x,
		r.y,
		r.z
	].every((e) => e >= -1e6 && e <= 1e6)) return { error: A("admin.zones.centreRange") };
	let i = n(e.radius);
	if (!Number.isFinite(i) || i < 1 || i > 2e3) return { error: A("admin.zones.radiusRange") };
	if (!e.deny.length || e.deny.some((e) => !Si.includes(e))) return { error: A("admin.zones.pickDeny", { actions: Si.join(", ") }) };
	if (e.owners.length > 64) return { error: A("admin.zones.ownersMax") };
	for (let t of e.owners) {
		let e = ai(t, !0);
		if (e) return { error: e };
	}
	let a = [...new Set(e.deny)].sort((e, t) => Si.indexOf(e) - Si.indexOf(t));
	return { args: {
		name: t,
		x: r.x,
		y: r.y,
		z: r.z,
		radius: i,
		deny: a,
		owners: [...new Set(e.owners)].sort(),
		allowAdmins: e.allowAdmins
	} };
}
var Oi = C((e) => ({
	draft: null,
	set: (t) => e({ draft: t })
})), ki = C((e) => ({
	focus: null,
	set: (t) => e({ focus: t })
})), Ai = "#version 300 es\nvoid main() {\n  gl_Position = vec4(gl_VertexID == 1 ? 3.0 : -1.0, gl_VertexID == 2 ? 3.0 : -1.0, 0.0, 1.0);\n}", ji = "#version 300 es\nprecision highp float;\nprecision highp int;\nprecision highp sampler2D;\nprecision highp sampler2DArray;\n\nuniform sampler2D u_nodes;        // coarse height, a node every 8 m, 0..1 = 0..4096 m\nuniform sampler2D u_cells;        // means over coarse cells: level 0 = 8 m, level 1 = 16 m\nuniform sampler2DArray u_fine;    // height offset, 1 m lattice, one layer per 1024 m tile, row by row from the north-west\nuniform sampler2DArray u_zones;   // R depth inside a zone, G zone level; 2 m lattice, same layers; see buildZonePyramid\n\nuniform vec2 u_origin;            // world position of the canvas's top-left corner: metres east and south of the north-west corner\nuniform float u_metresPerPixel;\nuniform float u_canvasHeight;\nuniform float u_pixelRatio;\nuniform int u_smoothLevel;        // lattice level for height, contours and broad shading (cell = 2^level m)\nuniform int u_fineLevel;          // lattice level for the shading's small features; at most u_smoothLevel\nuniform int u_zoneLevel;          // lattice level for the zones (cell = 2 * 2^level m)\nuniform float u_reliefGain;       // keeps the hillshade's contrast when a coarser level flattens the slopes\nuniform float u_contourStep;      // metres between the finest contour lines\nuniform float u_flame;            // zones above this level are dangerous\nuniform bool u_showZones;\nuniform float u_clock;            // seconds\n\nout vec4 o_colour;\n\nconst float WORLD_METRES = 10240.0;\nconst float NODE_METRES = 8.0;\nconst float NODE_RANGE = 4096.0;\nconst float FINE_RANGE = 255.0;\nconst float OUTER_LEVEL = 15.5;   // the Shroud beyond the playable land is level 16\nconst float STAIR_SLOPE = 0.44;   // how steep one 8-bit stair still looks on the 1 m lattice (half that a level up)\n\n// ---- lattice access -----------------------------------------------------------------------------------------------\n\n// 10 x 10 tiles of 2^bits samples: the texel and layer of one sample of the whole lattice, clamped to the world.\nivec3 inTile(ivec2 p, int bits) {\n  ivec2 q = clamp(p, ivec2(0), ivec2((10 << bits) - 1));\n  ivec2 tile = q >> bits;\n  return ivec3(q - (tile << bits), tile.y * 10 + tile.x);\n}\n\n// A quadratic B-spline along one axis at lattice position t: the first of its three samples, their weights and the\n// weights of the slope (per lattice cell). The result has a continuous slope.\nstruct Spline { int first; vec3 value; vec3 slope; };\n\nSpline splineAt(float t) {\n  float centre = floor(t + 0.5), f = t - centre;\n  float a = 0.5 - f, b = 0.5 + f;\n  return Spline(int(centre) - 1, vec3(0.5 * a * a, 0.75 - f * f, 0.5 * b * b), vec3(-a, -2.0 * f, b));\n}\n\n// Rewrites a spline over lattice samples as one over coarse nodes: each sample is bilinear between two nodes. A cell of\n// up to 4 m spans at most three nodes. nudge lines the axis up: a node sits on lattice sample 8i across, 8i - 1 down.\nSpline onNodes(Spline s, float cell, float nudge) {\n  Spline n = Spline(0, vec3(0.0), vec3(0.0));\n  for (int i = 0; i < 3; i++) {\n    float at = ((float(s.first + i) + 0.5) * cell - 0.5 + nudge) / NODE_METRES;\n    float whole = floor(at), part = at - whole;\n    if (i == 0) n.first = int(whole);\n    int k = int(whole) - n.first;\n    n.value[k] += s.value[i] * (1.0 - part);\n    n.value[k + 1] += s.value[i] * part;\n    n.slope[k] += s.slope[i] * (1.0 - part);\n    n.slope[k + 1] += s.slope[i] * part;\n  }\n  return n;\n}\n\n// ---- ground ---------------------------------------------------------------------------------------------------------\n\nstruct Ground { float metres; vec2 slope; };\n\nGround groundAt(vec2 world, int level) {\n  float cell = float(1 << level);\n  vec2 t = world / cell - 0.5;\n  Spline sx = splineAt(t.x), sy = splineAt(t.y);\n  float metres = 0.0;\n  vec2 rise = vec2(0.0);\n  bool coarse = level >= 3;\n  int cellLevel = level - 3;\n  ivec2 cellLast = ivec2((1280 >> max(cellLevel, 0)) - 1);\n  for (int j = 0; j < 3; j++) {\n    for (int i = 0; i < 3; i++) {\n      ivec2 p = ivec2(sx.first + i, sy.first + j);\n      float h = (texelFetch(u_fine, inTile(p, 10 - level), level).r - 0.5) * FINE_RANGE;\n      if (coarse) h += texelFetch(u_cells, clamp(p, ivec2(0), cellLast), cellLevel).r * NODE_RANGE;\n      metres += sx.value[i] * sy.value[j] * h;\n      rise += vec2(sx.slope[i] * sy.value[j], sx.value[i] * sy.slope[j]) * h;\n    }\n  }\n  if (!coarse) {\n    Spline nx = onNodes(sx, cell, 0.0), ny = onNodes(sy, cell, 1.0);\n    for (int j = 0; j < 3; j++) {\n      for (int i = 0; i < 3; i++) {\n        float h = texelFetch(u_nodes, clamp(ivec2(nx.first + i, ny.first + j), ivec2(0), ivec2(1279)), 0).r * NODE_RANGE;\n        metres += nx.value[i] * ny.value[j] * h;\n        rise += vec2(nx.slope[i] * ny.value[j], nx.value[i] * ny.slope[j]) * h;\n      }\n    }\n  }\n  return Ground(metres, rise / cell);\n}\n\n// The fine offsets are 8-bit, a metre per step, so a gentle slope is stored as a staircase, and the texture compression\n// adds blocks on top. Up close that would be all you see. So close-ups keep a smooth ground from a coarser level for the\n// height and the contours, and the shading adds back only the part of the fine slope that stands out from the smooth one\n// by more than a stair could: walls and cliffs stay, the stairs go.\nvec2 withoutStairs(vec2 fine, vec2 smoothed) {\n  vec2 extra = fine - smoothed;\n  float size = length(extra);\n  float stair = STAIR_SLOPE / float(1 << u_fineLevel);\n  return smoothed + extra * (max(0.0, size - stair) / max(size, 1e-4));\n}\n\n// ---- colour ---------------------------------------------------------------------------------------------------------\n\nconst int RAMP_STOPS = 9;\nconst float RAMP_METRES[RAMP_STOPS] = float[RAMP_STOPS](530.0, 610.0, 720.0, 860.0, 1040.0, 1300.0, 1650.0, 2050.0, 2420.0);\nconst vec3 RAMP_COLOUR[RAMP_STOPS] = vec3[RAMP_STOPS](\n  vec3(0.098, 0.094, 0.096), vec3(0.150, 0.134, 0.118), vec3(0.238, 0.203, 0.154), vec3(0.340, 0.272, 0.192),\n  vec3(0.425, 0.315, 0.208), vec3(0.505, 0.362, 0.245), vec3(0.625, 0.508, 0.398), vec3(0.792, 0.742, 0.668),\n  vec3(0.930, 0.908, 0.858));\n\nvec3 landColour(float metres) {\n  for (int i = 1; i < RAMP_STOPS; i++) {\n    if (metres < RAMP_METRES[i]) {\n      float a = clamp((metres - RAMP_METRES[i - 1]) / (RAMP_METRES[i] - RAMP_METRES[i - 1]), 0.0, 1.0);\n      return mix(RAMP_COLOUR[i - 1], RAMP_COLOUR[i], a);\n    }\n  }\n  return RAMP_COLOUR[RAMP_STOPS - 1];\n}\n\n// Light on a slope, 1 on flat ground: a sun in the north-west and a weaker fill on either side of it. The light wraps\n// past the point where a slope turns away from the sun, so steep shaded ground keeps its form instead of going flat.\nfloat sunlight(vec2 slope) {\n  vec3 normal = normalize(vec3(-slope, 1.0));\n  float key = dot(normal, vec3(-0.5417, -0.5417, 0.6428));\n  float west = dot(normal, vec3(-0.7077, 0.2932, 0.6428));\n  float north = dot(normal, vec3(0.2932, -0.7077, 0.6428));\n  float raw = 0.62 * key + 0.19 * west + 0.19 * north;\n  return max(0.0, raw + 0.42) / (0.6428 + 0.42);\n}\n\nvec3 shadeLand(vec3 colour, vec2 slope) {\n  float light = sunlight(slope * u_reliefGain);\n  vec3 lit = light < 1.0 ? mix(vec3(0.30, 0.26, 0.32), vec3(1.0), light) : mix(vec3(1.0), vec3(1.36, 1.28, 1.14), min(1.0, (light - 1.0) * 2.0));\n  return colour * lit;\n}\n\n// ---- contours -------------------------------------------------------------------------------------------------------\n\n// Coverage of the nearest line of one class. climb = metres of height per pixel, so metres / climb = pixels. minApart\n// and fullApart: how far apart, in CSS pixels, the class's lines must be to start showing and to show fully.\nfloat contourClass(float metres, float climb, float interval, float halfWidth, float minApart, float fullApart) {\n  float nearest = abs(metres / interval - floor(metres / interval + 0.5)) * interval;\n  float line = 1.0 - smoothstep(halfWidth - 0.6, halfWidth + 0.6, nearest / climb);\n  return line * smoothstep(minApart, fullApart, interval / climb / u_pixelRatio);\n}\n\nvec3 drawContours(vec3 colour, Ground g) {\n  float grade = length(g.slope);\n  float climb = max(grade * u_metresPerPixel, 1e-4);\n  float visible = smoothstep(0.003, 0.012, grade);\n  float hair = contourClass(g.metres, climb, u_contourStep * 0.25, 0.26 * u_pixelRatio, 26.0, 60.0);\n  float fine = contourClass(g.metres, climb, u_contourStep, 0.30 * u_pixelRatio, 5.0, 11.0);\n  float major = contourClass(g.metres, climb, u_contourStep * 5.0, 0.50 * u_pixelRatio, 5.0, 11.0);\n  float index = contourClass(g.metres, climb, u_contourStep * 25.0, 0.68 * u_pixelRatio, 5.0, 11.0);\n  colour = mix(colour, vec3(1.00, 0.94, 0.86), 0.10 * hair * visible);\n  colour = mix(colour, vec3(1.00, 0.93, 0.82), 0.20 * fine * visible);\n  colour = mix(colour, vec3(1.00, 0.90, 0.76), 0.36 * major * visible);\n  colour = mix(colour, vec3(1.00, 0.70, 0.42), 0.50 * index * visible);\n  return colour;\n}\n\n// ---- Shroud ---------------------------------------------------------------------------------------------------------\n\nstruct Veil { float depth; float danger; float outer; };\n\n// depth = mean distance signal; danger and outer = shares of the zone samples under this pixel. Samples outside every\n// zone do not vote, so a zone keeps its class right up to its border.\nVeil veilAt(vec2 world) {\n  float cell = 2.0 * float(1 << u_zoneLevel);\n  vec2 t = world / cell - 0.5;\n  Spline sx = splineAt(t.x), sy = splineAt(t.y);\n  Veil v = Veil(0.0, 0.0, 0.0);\n  float votes = 0.0;\n  for (int j = 0; j < 3; j++) {\n    for (int i = 0; i < 3; i++) {\n      vec2 z = texelFetch(u_zones, inTile(ivec2(sx.first + i, sy.first + j), 9 - u_zoneLevel), u_zoneLevel).rg;\n      float w = sx.value[i] * sy.value[j];\n      v.depth += w * z.r;\n      if (z.r > 0.004) {\n        float level = z.g * 255.0;\n        votes += w;\n        if (level > OUTER_LEVEL) v.outer += w;\n        else if (level > u_flame + 0.5) v.danger += w;\n      }\n    }\n  }\n  float total = max(votes, 1e-4);\n  v.danger /= total;\n  v.outer /= total;\n  return v;\n}\n\nfloat scatter(ivec2 p) {\n  uvec2 q = uvec2(p) * uvec2(1597334677u, 3812015801u);\n  uint h = (q.x ^ q.y) * 1597334677u;\n  return float(h >> 8) / 16777215.0;\n}\n\nfloat softNoise(vec2 q) {\n  vec2 whole = floor(q), f = q - whole;\n  f = f * f * (3.0 - 2.0 * f);\n  ivec2 p = ivec2(whole);\n  return mix(mix(scatter(p), scatter(p + ivec2(1, 0)), f.x), mix(scatter(p + ivec2(0, 1)), scatter(p + ivec2(1, 1)), f.x), f.y);\n}\n\n// Drifting cloud, about 300 pixels across at every zoom: two neighbouring scales cross-fade as the zoom changes.\nfloat mist(vec2 world) {\n  float scale = log2(u_metresPerPixel * 180.0), whole = floor(scale), between = scale - whole;\n  float size = exp2(whole);\n  vec2 wind = u_clock * vec2(0.016, 0.009);\n  float near = softNoise(world / size + wind) * 0.65 + softNoise(world / (size * 0.5) - wind * 1.7) * 0.35;\n  float far = softNoise(world / (size * 2.0) + wind * 0.6) * 0.65 + softNoise(world / size + wind) * 0.35;\n  return mix(near, far, between);\n}\n\n// Diagonal lines about 14-28 pixels apart. They stay on fixed world positions: every doubling of the zoom fades in a\n// line between each pair, so nothing jumps.\nfloat hatch(vec2 world) {\n  float scale = log2(u_metresPerPixel * 14.0 * u_pixelRatio), whole = ceil(scale);\n  float apart = exp2(whole), wide = smoothstep(0.55, 1.0, whole - scale);\n  float along = (world.x + world.y) * 0.7071 / apart;\n  float halfWidth = 0.7 * u_pixelRatio;\n  float toLine = abs(along - floor(along + 0.5)) * apart / u_metresPerPixel;\n  float toMiddle = (0.5 - abs(along - floor(along + 0.5))) * apart / u_metresPerPixel;\n  return max(1.0 - smoothstep(halfWidth - 0.6, halfWidth + 0.6, toLine), wide * (1.0 - smoothstep(halfWidth - 0.6, halfWidth + 0.6, toMiddle)));\n}\n\nvec3 drawVeil(vec3 colour, vec2 world, Veil v) {\n  float cover = smoothstep(0.0, 0.5, v.depth);\n  if (cover <= 0.0) return colour;\n  float edge = cover * (1.0 - smoothstep(0.12, 0.62, v.depth));\n  float cloud = mist(world);\n\n  float mistAlpha = cover * (0.20 + 0.26 * cloud);\n  vec3 cold = mix(colour, vec3(0.40, 0.55, 0.78), mistAlpha);\n  cold = mix(cold, vec3(0.62, 0.78, 0.95), 0.32 * edge);\n\n  float breath = 0.94 + 0.06 * sin(u_clock * 0.9);\n  float redAlpha = cover * (0.26 + 0.20 * cloud) * breath;\n  vec3 hot = mix(colour, vec3(0.86, 0.20, 0.08), redAlpha);\n  hot = mix(hot, vec3(1.00, 0.46, 0.16), cover * 0.34 * hatch(world));\n  hot = mix(hot, vec3(1.00, 0.52, 0.18), 0.62 * edge);\n\n  float darkAlpha = cover * (0.84 + 0.05 * cloud);\n  vec3 dark = mix(colour, vec3(0.040, 0.034, 0.032), darkAlpha);\n  dark = mix(dark, vec3(0.20, 0.17, 0.15), 0.30 * edge);\n\n  vec3 inside = mix(cold, hot, v.danger);\n  return mix(inside, dark, v.outer);\n}\n\n// ---- the picture ----------------------------------------------------------------------------------------------------\n\nvoid main() {\n  vec2 world = u_origin + vec2(gl_FragCoord.x, u_canvasHeight - gl_FragCoord.y) * u_metresPerPixel;\n  if (any(lessThan(world, vec2(0.0))) || any(greaterThanEqual(world, vec2(WORLD_METRES)))) {\n    o_colour = vec4(0.0);\n    return;\n  }\n  Ground ground = groundAt(world, u_smoothLevel);\n  vec2 slope = u_fineLevel < u_smoothLevel ? withoutStairs(groundAt(world, u_fineLevel).slope, ground.slope) : ground.slope;\n  vec3 colour = shadeLand(landColour(ground.metres), slope);\n  colour = drawContours(colour, ground);\n  if (u_showZones) colour = drawVeil(colour, world, veilAt(world));\n  // half a step of 8-bit dither keeps the dark gradients from banding\n  colour += (scatter(ivec2(gl_FragCoord.xy)) - 0.5) / 255.0;\n  o_colour = vec4(colour, 1.0);\n}", Mi = "#version 300 es\nprecision highp float;\nprecision highp int;\nprecision highp sampler2DArray;\nuniform sampler2DArray u_source;\nuniform int u_layer;\nout vec4 o_colour;\nvoid main() {\n  ivec3 p = ivec3(ivec2(gl_FragCoord.xy) * 2, u_layer);\n  vec2 a = texelFetch(u_source, p, 0).rg;\n  vec2 b = texelFetch(u_source, p + ivec3(1, 0, 0), 0).rg;\n  vec2 c = texelFetch(u_source, p + ivec3(0, 1, 0), 0).rg;\n  vec2 d = texelFetch(u_source, p + ivec3(1, 1, 0), 0).rg;\n  o_colour = vec4((a.r + b.r + c.r + d.r) * 0.25, max(max(a.g, b.g), max(c.g, d.g)), 0.0, 1.0);\n}", Ni = 10, Pi = 10240, Fi = 5, Ii = 512, Li = 4;
function Ri(e, t, n) {
	let r = e.createShader(t);
	e.shaderSource(r, n), e.compileShader(r);
	let i = e.getShaderInfoLog(r)?.trim();
	if (!e.getShaderParameter(r, e.COMPILE_STATUS)) throw Error(i || "shader");
	return i && console.warn(`relief shader: ${i}`), r;
}
function zi(e, t) {
	let n = e.createProgram();
	e.attachShader(n, Ri(e, e.VERTEX_SHADER, Ai)), e.attachShader(n, Ri(e, e.FRAGMENT_SHADER, t)), e.linkProgram(n);
	let r = e.getProgramInfoLog(n)?.trim();
	if (!e.getProgramParameter(n, e.LINK_STATUS)) throw Error(r || "link");
	return r && console.warn(`relief shader: ${r}`), n;
}
function Bi(e, t, n) {
	let r = e.createTexture();
	e.bindTexture(e.TEXTURE_2D, r), e.texStorage2D(e.TEXTURE_2D, 2, e.R32F, n, n);
	let i = n - 1, a = new Float32Array(n * n);
	for (let e = 0; e < n; e++) {
		let r = Math.min(e + 1, i) * n;
		for (let o = 0; o < n; o++) {
			let s = Math.min(o + 1, i);
			a[e * n + o] = (t[e * n + o] + t[e * n + s] + t[r + o] + t[r + s]) / 4;
		}
	}
	let o = n >> 1, s = new Float32Array(o * o);
	for (let e = 0; e < o; e++) for (let t = 0; t < o; t++) {
		let r = 2 * e * n + 2 * t;
		s[e * o + t] = (a[r] + a[r + 1] + a[r + n] + a[r + n + 1]) / 4;
	}
	e.texSubImage2D(e.TEXTURE_2D, 0, 0, 0, n, n, e.RED, e.FLOAT, a), e.texSubImage2D(e.TEXTURE_2D, 1, 0, 0, o, o, e.RED, e.FLOAT, s), e.texParameteri(e.TEXTURE_2D, e.TEXTURE_MIN_FILTER, e.NEAREST_MIPMAP_NEAREST), e.texParameteri(e.TEXTURE_2D, e.TEXTURE_MAG_FILTER, e.NEAREST);
}
function Vi(e, t, n) {
	let r = zi(e, Mi), i = e.createFramebuffer();
	e.useProgram(r), e.uniform1i(e.getUniformLocation(r, "u_source"), n);
	let a = e.getUniformLocation(r, "u_layer");
	e.bindFramebuffer(e.FRAMEBUFFER, i);
	for (let n = 1; n < Li; n++) {
		e.texParameteri(e.TEXTURE_2D_ARRAY, e.TEXTURE_BASE_LEVEL, n - 1), e.texParameteri(e.TEXTURE_2D_ARRAY, e.TEXTURE_MAX_LEVEL, n - 1), e.viewport(0, 0, Ii >> n, Ii >> n);
		for (let r = 0; r < 100; r++) e.framebufferTextureLayer(e.FRAMEBUFFER, e.COLOR_ATTACHMENT0, t, n, r), e.uniform1i(a, r), e.drawArrays(e.TRIANGLES, 0, 3);
	}
	e.texParameteri(e.TEXTURE_2D_ARRAY, e.TEXTURE_BASE_LEVEL, 0), e.texParameteri(e.TEXTURE_2D_ARRAY, e.TEXTURE_MAX_LEVEL, 3), e.bindFramebuffer(e.FRAMEBUFFER, null), e.deleteFramebuffer(i), e.deleteProgram(r);
}
async function Hi(e) {
	let t = await fetch(e);
	if (!t.ok) throw Error(`${e}: ${t.status}`);
	return createImageBitmap(await t.blob(), {
		premultiplyAlpha: "none",
		colorSpaceConversion: "none"
	});
}
var Ui = {
	base: null,
	tiles: /* @__PURE__ */ new Map(),
	root: "",
	tileKey(e, t) {
		let n = e / Pi, r = (Pi - t) / Pi, i = Math.min(9, Math.max(0, Math.floor(n * Ni))), a = Math.min(9, Math.max(0, Math.floor(r * Ni)));
		return {
			key: `${i}_${9 - a}`,
			lx: n * Ni - i,
			ly: r * Ni - a
		};
	},
	baseAt(e, t) {
		let n = this.base;
		if (!n) return null;
		let r = e * 1280 - .5, i = t * 1280 - .5, a = Math.floor(r), o = Math.floor(i), s = r - a, c = i - o, l = (e, t) => n[Math.min(1279, Math.max(0, t)) * 1280 + Math.min(1279, Math.max(0, e))], u = l(a, o), d = l(a + 1, o), f = l(a, o + 1), p = l(a + 1, o + 1);
		return ((u * (1 - s) + d * s) * (1 - c) + (f * (1 - s) + p * s) * c) * 4096;
	},
	quick(e, t) {
		if (!this.base || e < 0 || t < 0 || e > 10240 || t > 10240) return null;
		let n = e / Pi, r = (Pi - t) / Pi, i = this.baseAt(n + .00390625 / Ni, r + .00390625 / Ni), a = this.tileKey(e, t), o = this.tiles.get(a.key);
		return o?.data && (i += (o.data[Math.min(1023, Math.floor(a.ly * 1024)) * 1024 + Math.min(1023, Math.floor(a.lx * 1024))] / 255 - .5) * 255), i;
	},
	async exact(e, t) {
		if (!this.base) return null;
		let n = this.tileKey(e, t), r = this.tiles.get(n.key);
		if (!r) {
			let e;
			e = (async () => {
				let t = await Hi(`${this.root}gpu/d/${n.key}.webp`), r = new OffscreenCanvas(1024, 1024).getContext("2d", { willReadFrequently: !0 });
				r.drawImage(t, 0, 0);
				let i = r.getImageData(0, 0, 1024, 1024).data, a = /* @__PURE__ */ new Uint8Array(1048576);
				for (let e = 0, t = 0; e < i.length; e += 4) a[t++] = i[e];
				return e.data = a, this.tiles.size > 6 && this.tiles.delete(this.tiles.keys().next().value), a;
			})(), r = e, this.tiles.set(n.key, r);
		}
		try {
			await r;
		} catch {
			this.tiles.delete(n.key);
		}
		return this.quick(e, t);
	}
}, Wi = class extends k.Layer {
	canvas;
	gl;
	prog;
	u;
	ready = !1;
	timer = null;
	frame = 0;
	repeats = 0;
	fence = null;
	t = 0;
	lmap = null;
	constructor(e) {
		super(e), k.setOptions(this, e);
	}
	onAdd(e) {
		this.lmap = e;
		let t = this.canvas = k.DomUtil.create("canvas", "ka-gamemap");
		t.style.visibility = "hidden", e.getPane("tilePane").appendChild(t);
		let n = t.getContext("webgl2", {
			antialias: !1,
			premultipliedAlpha: !1,
			preserveDrawingBuffer: !1
		});
		if (!n) return this.options.onError?.(A("admin.map.noWebgl2")), this;
		this.gl = n, t.addEventListener("webglcontextlost", (e) => {
			e.preventDefault(), this.ready = !1, this.options.onError?.(A("admin.map.contextLost"));
		});
		let r;
		try {
			r = zi(n, ji);
		} catch (e) {
			return console.error(e), this.options.onError?.(String(e.message ?? e)), this;
		}
		this.prog = r, n.useProgram(r);
		let i = /* @__PURE__ */ new Map();
		return this.u = (e) => (i.has(e) || i.set(e, n.getUniformLocation(r, e)), i.get(e)), Ui.root = this.options.root, this.load().then(() => {
			this.ready = !0, t.style.visibility = "", this.redraw(), this.options.onReady?.();
		}).catch((e) => {
			console.error(e), this.options.onError?.(String(e?.message ?? e));
		}), e.on("move zoom viewreset resize", this.redraw, this), this.resume(), this;
	}
	onRemove(e) {
		return this.pause(), e.off("move zoom viewreset resize", this.redraw, this), this.canvas?.remove(), this.lmap = null, this;
	}
	resume() {
		this.pause(), this.timer = setInterval(() => {
			if (document.hidden || !this.ready) return;
			let e = this.options.animate && this.options.showFog;
			if (!e && !this.repeats) return;
			let t = this.gl;
			if (this.fence) {
				if (t.clientWaitSync(this.fence, 0, 0) === t.TIMEOUT_EXPIRED) return;
				t.deleteSync(this.fence), this.fence = null;
			}
			e && (this.t = performance.now()), this.repeats && this.repeats--, this.schedule();
		}, 125);
	}
	pause() {
		this.timer && clearInterval(this.timer), this.timer = null, cancelAnimationFrame(this.frame), this.frame = 0;
	}
	setLook(e) {
		k.setOptions(this, e), this.redraw();
	}
	async load() {
		let e = this.gl, t = this.options.root, n = 0, r = () => this.options.onProgress?.(++n, 204);
		e.pixelStorei(e.UNPACK_ALIGNMENT, 1), e.pixelStorei(e.UNPACK_COLORSPACE_CONVERSION_WEBGL, e.NONE), e.pixelStorei(e.UNPACK_PREMULTIPLY_ALPHA_WEBGL, !1);
		let i = await fetch(`${t}gpu/base.bin`);
		if (!i.ok || !i.body) throw Error(`gpu/base.bin: ${i.status}`);
		r();
		let a = new Uint16Array(await new Response(i.body.pipeThrough(new DecompressionStream("gzip"))).arrayBuffer()), o = new Float32Array(a.length);
		for (let e = 0; e < a.length; e++) o[e] = a[e] / 65535;
		Ui.base = o, e.activeTexture(e.TEXTURE0), e.bindTexture(e.TEXTURE_2D, e.createTexture()), e.texImage2D(e.TEXTURE_2D, 0, e.R32F, 1280, 1280, 0, e.RED, e.FLOAT, o), e.texParameteri(e.TEXTURE_2D, e.TEXTURE_MIN_FILTER, e.NEAREST), e.texParameteri(e.TEXTURE_2D, e.TEXTURE_MAG_FILTER, e.NEAREST), e.activeTexture(e.TEXTURE1), Bi(e, o, 1280), r();
		let s = async (n, i, a, o, s, c) => {
			e.activeTexture(e.TEXTURE0 + n);
			let l = e.createTexture();
			e.bindTexture(e.TEXTURE_2D_ARRAY, l), e.texStorage3D(e.TEXTURE_2D_ARRAY, o, s, a, a, 100), e.texParameteri(e.TEXTURE_2D_ARRAY, e.TEXTURE_MIN_FILTER, e.NEAREST_MIPMAP_NEAREST), e.texParameteri(e.TEXTURE_2D_ARRAY, e.TEXTURE_MAG_FILTER, e.NEAREST);
			let u = [];
			for (let e = 0; e < Ni; e++) for (let t = 0; t < Ni; t++) u.push([e, t]);
			let d = 0, f = async () => {
				for (; d < u.length;) {
					let [o, s] = u[d++], f = await Hi(`${t}gpu/${i}/${o}_${s}.webp`);
					e.activeTexture(e.TEXTURE0 + n), e.bindTexture(e.TEXTURE_2D_ARRAY, l), e.texSubImage3D(e.TEXTURE_2D_ARRAY, 0, 0, 0, (9 - s) * Ni + o, a, a, 1, c, e.UNSIGNED_BYTE, f), f.close?.(), r();
				}
			};
			return await Promise.all([
				f(),
				f(),
				f(),
				f(),
				f(),
				f()
			]), l;
		};
		await s(2, "d", 1024, Fi, e.R8, e.RED), e.activeTexture(e.TEXTURE2), e.generateMipmap(e.TEXTURE_2D_ARRAY), r(), Vi(e, await s(3, "f", Ii, Li, e.RG8, e.RG), 3), r(), e.useProgram(this.prog), [
			"u_nodes",
			"u_cells",
			"u_fine",
			"u_zones"
		].forEach((t, n) => e.uniform1i(this.u(t), n));
	}
	redraw() {
		this.repeats = 2, this.schedule();
	}
	schedule() {
		!this.frame && this.ready && (this.frame = requestAnimationFrame(() => {
			this.frame = 0, this.draw();
		}));
	}
	draw() {
		let e = this.lmap;
		if (!this.ready || !e) return;
		let t = this.canvas, n = this.gl, r = e.getSize(), i = window.devicePixelRatio || 1;
		if (!r.x || !r.y) return;
		(t.width !== Math.round(r.x * i) || t.height !== Math.round(r.y * i)) && (t.width = Math.round(r.x * i), t.height = Math.round(r.y * i), t.style.width = r.x + "px", t.style.height = r.y + "px"), k.DomUtil.setPosition(t, e.containerPointToLayerPoint([0, 0]));
		let a = e.project(e.containerPointToLatLng([0, 0]), 5), o = e.getZoomScale(5, e.getZoom()) / i, s = Math.log2(o), c = Math.max(2, Math.min(4, Math.round(s))), l = s < -.6 ? 1 : Math.max(0, Math.min(c, Math.round(s)));
		n.viewport(0, 0, t.width, t.height);
		let u = this.u;
		n.uniform2f(u("u_origin"), a.x, a.y), n.uniform1f(u("u_metresPerPixel"), o), n.uniform1f(u("u_canvasHeight"), t.height), n.uniform1f(u("u_pixelRatio"), i), n.uniform1i(u("u_smoothLevel"), c), n.uniform1i(u("u_fineLevel"), l), n.uniform1i(u("u_zoneLevel"), Math.max(0, Math.min(3, Math.round(s - 1)))), n.uniform1f(u("u_reliefGain"), 1.22 ** (c - 2) * 1.5), n.uniform1f(u("u_contourStep"), this.options.isolineSpacing), n.uniform1f(u("u_flame"), this.options.playerFogLevel), n.uniform1i(u("u_showZones"), +!!this.options.showFog), n.uniform1f(u("u_clock"), this.t / 1e3), n.drawArrays(n.TRIANGLES, 0, 3), this.fence || (this.fence = n.fenceSync(n.SYNC_GPU_COMMANDS_COMPLETE, 0), n.flush());
	}
}, Gi = 5;
function Ki(e, t) {
	let n = (e.langs ?? []).map((e) => e.code);
	if (t && n.includes(t)) return t;
	let r = typeof navigator < "u" ? [...navigator.languages ?? [], navigator.language].filter(Boolean) : [];
	for (let e of r) {
		let t = n.find((t) => t.toLowerCase() === e.toLowerCase());
		if (t) return t;
		let r = e.split("-")[0].toLowerCase(), i = n.find((e) => e.split("-")[0].toLowerCase() === r);
		if (i) return i;
	}
	return e.lang;
}
var qi = (e) => e.source === "game" ? `game:${e.build ?? ""}` : "copy", Ji = {
	spawns: "#e0533a",
	gather: "#f2c14e",
	ore: "#5fd3c4",
	chests: "#f2c14e"
}, Yi = {
	1: "p-chest-gold.webp",
	2: "p-chest-silver.webp",
	3: "p-chest-wood.webp"
};
function Xi(e) {
	switch (e) {
		case 1: return A("admin.map.chestGold");
		case 2: return A("admin.map.chestSilver");
		case 3: return A("admin.map.chestWood");
		default: return A("admin.map.chest");
	}
}
var X = (e) => String(e ?? "").replace(/[&<>"']/g, (e) => ({
	"&": "&amp;",
	"<": "&lt;",
	">": "&gt;",
	"\"": "&quot;",
	"'": "&#39;"
})[e]), Z = (e) => e.y == null ? A("admin.map.posXZ", {
	x: Math.round(e.x),
	z: Math.round(e.z)
}) : A("admin.map.posXZH", {
	x: Math.round(e.x),
	z: Math.round(e.z),
	h: Math.round(e.y)
}), Zi = (e, t) => t ? A("admin.map.withLevel", {
	label: e,
	level: t
}) : e, Qi = (e, t = "", n = "") => `<b>${X(e)}</b>${t ? `<span>${t}</span>` : ""}${n}`, $i = (e) => Math.max(12, Math.min(40, 6 + e * 5)), ea = class extends k.DivIcon {
	hp;
	constructor(e, t) {
		super(e), this.hp = t;
	}
	createIcon(e) {
		let t = super.createIcon(e);
		return this.hp != null && t.querySelector("em")?.style.setProperty("--hp", this.hp), t;
	}
}, ta = k.CircleMarker.extend({
	options: {
		scale: 1,
		stroke: !1,
		fill: !1,
		ring: null,
		item: !1,
		opacity: 1,
		dot: null
	},
	_project() {
		this._radius = $i(this._map.getZoom()) * (this.options.scale ?? 1) / 2, k.CircleMarker.prototype._project.call(this);
	},
	_updatePath() {
		let e = this.options, t = this._point, n = this._renderer._ctx, r = this._radius * 2;
		if (!n) return;
		if (n.save(), n.globalAlpha = e.opacity ?? 1, e.dot && !e.img) {
			n.beginPath(), n.arc(t.x, t.y, Math.max(4, r * .22), 0, Math.PI * 2), n.fillStyle = e.dot, n.fill(), n.lineWidth = 1.2, n.strokeStyle = "#0b0b0b", n.stroke(), n.restore();
			return;
		}
		let i = e.img;
		if (!i) {
			n.restore();
			return;
		}
		if (!i.complete || !i.naturalWidth) {
			i.waiting.add(this), n.restore();
			return;
		}
		(e.item || e.ring) && (n.beginPath(), n.arc(t.x, t.y, r * .55, 0, Math.PI * 2), n.fillStyle = "#0e0d0ccc", n.fill(), n.lineWidth = e.ring ? 2.2 : 1.5, n.strokeStyle = e.ring || "#c9a24a", e.ring && (n.shadowColor = e.ring, n.shadowBlur = 6), n.stroke(), n.shadowBlur = 0), n.shadowColor = "#000c", n.shadowBlur = 4, n.shadowOffsetY = 1;
		let a = e.item || e.ring ? .82 : 1;
		n.drawImage(i, t.x - r * a / 2, t.y - r * a / 2, r * a, r * a), n.restore();
	}
}), na = class {
	map;
	root;
	cb;
	game = null;
	D = null;
	F = null;
	dropsBy = {};
	PTS = null;
	CH = null;
	ORE = null;
	imgs = /* @__PURE__ */ new Map();
	icons;
	decor;
	catLayers = {};
	pointMk = [];
	labelLayer = null;
	finderLayer = null;
	live;
	enemyLayer;
	zoneLayer;
	worldLayer;
	players = /* @__PURE__ */ new Map();
	raf = 0;
	routeLine = null;
	routeEnd = null;
	flash = null;
	finderGen = 0;
	follow = null;
	route = null;
	active = !0;
	chestLv = [5, 50];
	surfaceOnly = !1;
	constructor(e, t, n, r) {
		this.root = t, this.cb = n;
		let i = this.map = k.map(e, {
			crs: k.CRS.Simple,
			minZoom: 1,
			maxZoom: 9,
			zoomSnap: .25,
			zoomDelta: .5,
			wheelPxPerZoomLevel: 90,
			zoomControl: !1,
			attributionControl: !1,
			preferCanvas: !0,
			maxBoundsViscosity: .8,
			zoomAnimation: !1,
			fadeAnimation: !1,
			doubleClickZoom: !0
		}), a = this.bounds();
		i.setMaxBounds(a.pad(.15)), k.tileLayer(`${t}tiles/{z}/{x}_{y}.webp`, {
			tileSize: 256,
			minNativeZoom: 0,
			maxNativeZoom: 3,
			noWrap: !0,
			bounds: a,
			keepBuffer: 4
		}).addTo(i), k.rectangle(a, {
			color: "#4d413a",
			weight: 1,
			fill: !1,
			interactive: !1
		}).addTo(i), r.relief && document.createElement("canvas").getContext("webgl2") && (n.loading(A("admin.map.loadingRelief")), this.game = new Wi({
			root: t,
			isolineSpacing: r.look.spacing,
			playerFogLevel: r.look.flame,
			showFog: r.look.fog,
			animate: r.look.animate,
			onProgress: (e, t) => n.loading(A("admin.map.loadingReliefPct", { pct: Math.round(e / t * 100) })),
			onReady: () => {
				n.loading(""), e.dataset.relief = "ready";
			},
			onError: (t) => {
				n.loading(""), e.dataset.relief = "error", n.error(A("admin.map.reliefUnavailable", { reason: t }));
			}
		}), this.game.addTo(i)), i.createPane("decor"), i.getPane("decor").style.zIndex = "450", i.getPane("decor").style.pointerEvents = "none", i.createPane("live"), i.getPane("live").style.zIndex = "650", this.decor = k.svg({ pane: "decor" }), this.icons = k.canvas({
			padding: .3,
			tolerance: 2
		}), this.zoneLayer = k.layerGroup().addTo(i), this.worldLayer = k.layerGroup().addTo(i), this.live = k.layerGroup().addTo(i), this.enemyLayer = k.layerGroup().addTo(i), r.view ? i.setView(this.toLL(r.view.x, r.view.z), r.view.zoom) : i.fitBounds(a), i.on("moveend", () => {
			let e = this.fromLL(i.getCenter());
			n.view({
				x: Math.round(e.x),
				z: Math.round(e.z),
				zoom: i.getZoom()
			});
		}), i.on("dragstart", () => {
			this.follow != null && n.stopFollow(), n.closeMenu();
		}), i.on("zoomstart movestart", () => n.closeMenu()), i.on("mousemove", (e) => this.showCoords(this.fromLL(e.latlng))), i.on("mouseout", () => n.coords(null)), i.on("contextmenu", (e) => this.spot(e)), i.on("click", (e) => this.spot(e));
		let o = () => {
			let t = i.getZoom();
			e.dataset.ltier = String(t >= 5.08 ? 3 : t >= 4.28 ? 2 : +(t >= 3.28)), this.layoutLabels();
		};
		i.on("zoomend", o), o();
	}
	destroy() {
		this.active = !1, cancelAnimationFrame(this.raf), this.game?.pause(), this.map.remove();
	}
	bounds() {
		return k.latLngBounds(this.map.unproject([0, Pi], Gi), this.map.unproject([Pi, 0], Gi));
	}
	toLL(e, t) {
		return this.map.unproject([e, Pi - t], Gi);
	}
	fromLL(e) {
		let t = this.map.project(e, Gi);
		return {
			x: t.x,
			z: Pi - t.y
		};
	}
	stop(e) {
		k.DomEvent.stop(e);
		let t = e.originalEvent;
		t && k.DomEvent.stop(t);
	}
	point(e) {
		let t = e.containerPoint ?? this.map.latLngToContainerPoint(e.latlng);
		return {
			x: t.x,
			y: t.y
		};
	}
	coordReq = 0;
	showCoords(e) {
		if (e.x < 0 || e.z < 0 || e.x > 10240 || e.z > 10240) return this.cb.coords(null);
		this.cb.coords({
			...e,
			y: Ui.quick(e.x, e.z)
		});
		let t = ++this.coordReq;
		Ui.base && setTimeout(() => {
			t === this.coordReq && Ui.exact(e.x, e.z);
		}, 250);
	}
	spot(e) {
		let t = this.fromLL(e.latlng);
		if (t.x < 0 || t.z < 0 || t.x > 10240 || t.z > 10240) return this.cb.closeMenu();
		this.cb.menu({
			kind: "spot",
			pos: {
				x: Math.round(t.x * 10) / 10,
				z: Math.round(t.z * 10) / 10,
				y: Ui.quick(t.x, t.z)
			}
		}, this.point(e));
	}
	setLook(e) {
		this.game?.setLook({
			isolineSpacing: e.spacing,
			playerFogLevel: e.flame,
			showFog: e.fog,
			animate: e.animate
		});
	}
	pause() {
		this.active = !1, this.game?.pause();
	}
	resume() {
		this.active = !0, this.game?.resume(), setTimeout(() => {
			this.map.invalidateSize(), this.game?.redraw();
		}, 0), this.startAnim();
	}
	img(e) {
		let t = this.imgs.get(e);
		if (!t) {
			t = new Image(), t.waiting = /* @__PURE__ */ new Set();
			let n = t;
			t.onload = () => {
				for (let e of n.waiting) e._map && e.redraw();
				n.waiting.clear();
			}, t.src = `${this.root}i/${e}`, this.imgs.set(e, t);
		}
		return t;
	}
	async getJson(e) {
		let t = await fetch(this.root + e).catch(() => null);
		return !t || !t.ok || !(t.headers.get("content-type") ?? "").includes("json") ? null : await t.json();
	}
	async loadMarkers(e) {
		let t = await this.getJson("game/markers.json") ?? await this.getJson("data/markers.json");
		if (!t || !Array.isArray(t.points)) throw Error(A("admin.map.noMarkerData"));
		this.D = t, this.lang = t.lang, this.english = {
			cats: t.cats.map((e) => e.label),
			points: t.points.map((e) => e[4]),
			labels: t.labels.map((e) => e[0])
		};
		for (let e of t.cats) this.catLayers[e.id] = k.layerGroup();
		t.points.forEach((e, n) => {
			let [r, i, a, o, , , , s] = e, c = t.cats[r], l = new ta(this.toLL(i, a), {
				renderer: this.icons,
				img: this.img(s || c.icon),
				item: !!s
			});
			l.bindTooltip(() => {
				let [, , , , e, s, c] = t.points[n];
				return Qi(e, `${X(Zi(t.cats[r].label, c))}${s ? ` · ${X(s)}` : ""}`, `<em>${X(Z({
					x: i,
					z: a,
					y: o
				}))}</em>`);
			}, {
				direction: "top",
				offset: [0, -8],
				className: "ka-tip"
			});
			let u = (e) => {
				this.stop(e), this.cb.menu({
					kind: "marker",
					pos: {
						x: i,
						z: a,
						y: o
					},
					name: t.points[n][4],
					sub: t.cats[r].label,
					idx: n
				}, this.point(e));
			};
			l.on("click", u), l.on("contextmenu", u), this.pointMk[n] = l;
		}), this.buildLabels();
		let n = Ki(t, e);
		return n && n !== t.lang && await this.setLang(n), t;
	}
	english = null;
	lang;
	async setLang(e) {
		let t = this.D, n = this.english;
		if (!t || !n || t.source !== "game") return !1;
		let r = e === t.lang ? n : await this.getJson(`game/lang/${encodeURIComponent(e)}.json`);
		if (!r || r.points.length !== t.points.length || r.cats.length !== t.cats.length || r.labels.length !== t.labels.length) return !1;
		t.cats.forEach((e, t) => {
			e.label = r.cats[t] || n.cats[t];
		}), t.points.forEach((e, t) => {
			e[4] = r.points[t] || n.points[t];
		}), t.labels.forEach((e, t) => {
			e[0] = r.labels[t] || n.labels[t];
		}), this.lang = e;
		let i = !!this.labelLayer && this.map.hasLayer(this.labelLayer);
		return this.labelLayer?.remove(), this.buildLabels(), i && this.labelLayer?.addTo(this.map), !0;
	}
	buildLabels() {
		let e = this.D;
		if (e) {
			this.labelLayer = k.layerGroup();
			for (let [t, n, r, i] of e.labels) k.marker(this.toLL(n, r), {
				interactive: !1,
				keyboard: !1,
				icon: k.divIcon({
					className: `ka-place t${i || 1}`,
					html: `<span>${X(t)}</span>`,
					iconSize: void 0
				})
			}).addTo(this.labelLayer);
		}
	}
	applyMarkers(e, t, n, r, i) {
		let a = this.D;
		if (!a) return;
		this.chestLv = i;
		let o = new Set(n);
		for (let e of Object.values(this.catLayers)) e.clearLayers();
		this.pointMk.forEach((e, t) => {
			let n = a.points[t], i = o.has(t);
			e.options.opacity = i ? .35 : 1, r && i || this.lvOut(n[6]) || this.catLayers[a.cats[n[0]].id].addLayer(e);
		});
		for (let t of a.cats) {
			let n = this.catLayers[t.id];
			e[t.id] ? n.addTo(this.map) : n.remove();
		}
		t ? this.labelLayer?.addTo(this.map) : this.labelLayer?.remove();
	}
	lvOut(e) {
		return !!e && (e < this.chestLv[0] || e > this.chestLv[1]);
	}
	async loadFind() {
		let e = await fetch(`${this.root}data/find.json`);
		if (!e.ok) throw Error(`data/find.json: ${e.status}`);
		let t = this.F = await e.json();
		this.dropsBy = {};
		for (let [e, n] of Object.entries(t.drops)) for (let t of n.by) (this.dropsBy[t] ??= []).push(e);
		return t;
	}
	async needFinderData(e) {
		let t = async (e) => {
			let t = await fetch(this.root + e);
			if (!t.ok) throw Error(`${e}: ${t.status}`);
			return t;
		}, n = new Set(e.map((e) => e.kind));
		if ((n.has("spawns") || n.has("gather")) && !this.PTS && (this.PTS = new Uint16Array(await (await t("data/pts.bin")).arrayBuffer())), n.has("chests") && !this.CH) {
			let e = await (await t("data/chests.json")).json();
			for (let t of Object.keys(e.items)) {
				let n = e.items[t];
				for (let e = 1; e < n.length; e++) n[e] += n[e - 1];
			}
			this.CH = e;
		}
		n.has("ore") && !this.ORE && (this.ORE = await (await t("data/ore.json")).json());
	}
	async setFinders(e, t) {
		let n = ++this.finderGen;
		this.surfaceOnly = t;
		let r = this.F;
		if (!r) return;
		try {
			await this.needFinderData(e);
		} catch (e) {
			this.cb.error(A("admin.map.finderData", { message: e.message }));
			return;
		}
		if (n !== this.finderGen) return;
		let i = k.layerGroup();
		for (let n of e) {
			let e = r[n.kind]?.[n.slug];
			if (!e) continue;
			let a = e.img ? this.img(e.img) : null, o = e.color || Ji[n.kind], s = (t, n, r, s) => {
				let c = new ta(this.toLL(t, n), {
					renderer: this.icons,
					img: a,
					dot: a ? null : o,
					...r
				});
				c.bindTooltip(s, {
					direction: "top",
					offset: [0, -8],
					className: "ka-tip"
				});
				let l = (i) => {
					this.stop(i), this.cb.menu({
						kind: "marker",
						pos: {
							x: t,
							z: n,
							y: null
						},
						name: e.n,
						sub: r.sub ?? ""
					}, this.point(i));
				};
				c.on("click", l), c.on("contextmenu", l), i.addLayer(c);
			};
			if ((n.kind === "spawns" || n.kind === "gather") && this.PTS && e.o) {
				let [t, r] = e.o, i = n.kind === "spawns" ? this.dropsBy[n.slug] ?? [] : [], a = n.kind === "spawns" ? e.boss ? A("admin.map.boss") : A("admin.map.creatureSpawn") : e.g ?? "", o = () => Qi(e.n, X(a), i.length ? `<em>${X(A("admin.map.drops", { list: `${i.slice(0, 8).join(", ")}${i.length > 8 ? "…" : ""}` }))}</em>` : "");
				for (let i = t; i < t + r; i++) s(this.PTS[2 * i] / 6, this.PTS[2 * i + 1] / 6, {
					item: n.kind === "gather",
					scale: e.boss ? 1.2 : .9,
					sub: a
				}, o);
			} else if (n.kind === "chests" && this.CH) for (let t of this.CH.items[n.slug] ?? []) {
				let [n, r, a, o, s] = this.CH.chests[t];
				if (this.lvOut(s)) continue;
				let c = new ta(this.toLL(n, r), {
					renderer: this.icons,
					img: this.img(Yi[o] ?? "p-chest-wood.webp"),
					scale: .9
				});
				c.bindTooltip(() => `${e.img ? `<img class="ka-tipimg" src="${this.root}i/${e.img}" alt="">` : ""}${Qi(e.n, X(Zi(Xi(o), s)), `<em>${X(Z({
					x: n,
					z: r,
					y: a
				}))}</em>`)}`, {
					direction: "top",
					offset: [0, -8],
					className: "ka-tip"
				});
				let l = (t) => {
					this.stop(t), this.cb.menu({
						kind: "marker",
						pos: {
							x: n,
							z: r,
							y: a
						},
						name: A("admin.map.chestOf", {
							chest: Xi(o),
							item: e.n
						}),
						sub: ""
					}, this.point(t));
				};
				c.on("click", l), c.on("contextmenu", l), i.addLayer(c);
			}
			else if (n.kind === "ore" && this.ORE) for (let [r, i, a, o, c] of this.ORE[n.slug] ?? []) (!t || o) && s(r, i, {
				scale: (16 + 10 * Math.min(1, Math.log10(Math.max(1, a)) / 4)) / 24,
				opacity: o ? 1 : .45,
				ring: n.slug.startsWith("archaic-essence") ? "#ff5ad8" : null,
				sub: A("admin.map.ore")
			}, () => {
				let t = a.toLocaleString(Be() ?? "en-US");
				return Qi(e.n, X(c > 0 ? A(o ? "admin.map.oreDepth" : "admin.map.oreBuriedDepth", {
					vol: t,
					depth: c
				}) : A(o ? "admin.map.oreVolume" : "admin.map.oreBuried", { vol: t })));
			});
		}
		this.finderLayer?.remove(), this.finderLayer = i.addTo(this.map);
	}
	flyTo(e, t, n) {
		this.follow != null && this.cb.stopFollow(), this.map.setView(this.toLL(e, t), Math.max(this.map.getZoom(), n), { animate: !1 }), this.flash?.remove(), this.flash = k.circleMarker(this.toLL(e, t), {
			radius: 26,
			color: "#f9c74f",
			weight: 3,
			fill: !1,
			className: "ka-flash",
			interactive: !1,
			pane: "decor",
			renderer: this.decor
		}).addTo(this.map), setTimeout(() => {
			this.flash?.remove(), this.flash = null;
		}, 4e3);
	}
	fit() {
		this.map.fitBounds(this.bounds());
	}
	playerIcon(e) {
		let t = e.healthMax ? Math.max(0, Math.min(1, (e.health ?? 0) / e.healthMax)) : null;
		return new ea({
			className: "ka-player",
			iconSize: [16, 16],
			iconAnchor: [8, 8],
			html: `<i></i><span>${X(e.name || A("admin.common.playerEntity", { entity: e.entity }))}${t == null ? "" : "<em></em>"}</span>`
		}, t == null ? null : `${Math.round(t * 100)}%`);
	}
	setPlayers(e) {
		let t = e.filter((e) => e.pos && Number.isFinite(e.pos.x)), n = /* @__PURE__ */ new Set(), r = performance.now();
		for (let e of t) {
			n.add(e.entity);
			let t = this.players.get(e.entity);
			if (!t) {
				let n = k.marker(this.toLL(e.pos.x, e.pos.z), {
					pane: "live",
					zIndexOffset: 9e3,
					icon: this.playerIcon(e),
					keyboard: !1
				}).bindTooltip(() => {
					let t = this.players.get(e.entity)?.p ?? e;
					return Qi(t.name || A("admin.common.playerEntity", { entity: t.entity }), X(Z(t.pos)), t.healthMax ? `<em>${X(A("admin.map.healthTip", {
						health: Math.round(t.health ?? 0),
						max: Math.round(t.healthMax)
					}))}</em>` : "");
				}, {
					direction: "top",
					offset: [0, -10],
					className: "ka-tip"
				}).addTo(this.live), i = (t) => {
					this.stop(t), this.cb.menu({
						kind: "player",
						entity: e.entity
					}, this.point(t));
				};
				n.on("click", i), n.on("contextmenu", i), t = {
					mk: n,
					from: { ...e.pos },
					to: { ...e.pos },
					cur: { ...e.pos },
					t0: r,
					key: "",
					p: e
				}, this.players.set(e.entity, t);
			}
			t.p = e;
			let i = `${e.name}|${e.healthMax ? Math.round((e.health ?? 0) / e.healthMax * 20) : ""}`;
			i !== t.key && (t.key = i, t.mk.setIcon(this.playerIcon(e))), (e.pos.x !== t.to.x || e.pos.z !== t.to.z) && (t.from = { ...t.cur }, t.to = { ...e.pos }, t.t0 = r);
		}
		for (let [e, t] of this.players) n.has(e) || (t.mk.remove(), this.players.delete(e));
		this.startAnim(), this.drawRoute(), this.layoutLabels();
	}
	playerPos(e) {
		return this.players.get(e)?.cur ?? null;
	}
	startAnim() {
		if (this.raf || !this.active) return;
		let e = () => {
			if (this.raf = 0, !this.active) return;
			let t = performance.now(), n = !1;
			for (let [e, r] of this.players) {
				let i = Math.min(1, (t - r.t0) / 950);
				r.cur = {
					x: r.from.x + (r.to.x - r.from.x) * i,
					y: r.to.y,
					z: r.from.z + (r.to.z - r.from.z) * i
				}, r.mk.setLatLng(this.toLL(r.cur.x, r.cur.z)), i < 1 && (n = !0), this.follow === e && this.map.panTo(this.toLL(r.cur.x, r.cur.z), {
					animate: !1,
					noMoveStart: !0
				});
			}
			this.route && this.drawRoute(), this.layoutLabels(), n && (this.raf = requestAnimationFrame(e));
		};
		this.raf = requestAnimationFrame(e);
	}
	layoutLabels() {
		let e = [];
		for (let t of this.players.values()) {
			let n = t.mk.getElement();
			if (!n) continue;
			let r = this.map.latLngToContainerPoint(t.mk.getLatLng()), i = e.filter((e) => Math.abs(e.x - r.x) < 90 && Math.abs(e.y - r.y) < 16).length;
			e.push(r), n.style.setProperty("--row", String(i));
		}
	}
	setWorld(e, t) {
		this.worldLayer.clearLayers();
		for (let t of e) {
			let e = k.marker(this.toLL(t.pos.x, t.pos.z), {
				pane: "live",
				zIndexOffset: 8e3,
				keyboard: !1,
				icon: k.divIcon({
					className: "ka-wp",
					iconSize: [26, 26],
					iconAnchor: [13, 24],
					html: `<img src="${this.root}i/g-mapmarker_waypoint.webp" alt=""><span>${X(t.name)}</span>`
				})
			}).bindTooltip(() => Qi(t.name, X(A("admin.map.waypoint")), `<em>${X(Z(t.pos))}</em>`), {
				direction: "top",
				offset: [0, -24],
				className: "ka-tip"
			}).addTo(this.worldLayer), n = (e) => {
				this.stop(e), this.cb.menu({
					kind: "waypoint",
					name: t.name,
					pos: t.pos
				}, this.point(e));
			};
			e.on("click", n), e.on("contextmenu", n);
		}
		for (let e of t) {
			let t = k.marker(this.toLL(e.pos.x, e.pos.z), {
				pane: "live",
				zIndexOffset: 7e3,
				keyboard: !1,
				icon: k.divIcon({
					className: "ka-grave",
					iconSize: [22, 22],
					iconAnchor: [11, 11],
					html: "<svg viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\"M7 21V9a5 5 0 0 1 10 0v12z\" /><path d=\"M12 9v6M9.5 11.5h5\" /></svg>"
				})
			}).bindTooltip(() => Qi(e.name == null ? A("admin.map.graveSomeone") : A("admin.map.graveOf", { name: e.name }), "", `<em>${X(Z(e.pos))}</em>`), {
				direction: "top",
				offset: [0, -10],
				className: "ka-tip"
			}).addTo(this.worldLayer), n = (t) => {
				this.stop(t), this.cb.menu({
					kind: "grave",
					...e
				}, this.point(t));
			};
			t.on("click", n), t.on("contextmenu", n);
		}
	}
	setZones(e, t) {
		this.zoneLayer.clearLayers();
		for (let t of e) {
			if (!t.pos) continue;
			k.circle(this.toLL(t.pos.x, t.pos.z), {
				radius: t.radius / 2 ** Gi,
				color: t.active === !1 ? "#94857a" : "#ff8540",
				weight: 2,
				dashArray: "6 4",
				fillColor: "#ff6a1a",
				fillOpacity: .08,
				pane: "decor",
				renderer: this.decor,
				interactive: !1
			}).addTo(this.zoneLayer);
			let e = k.marker(this.toLL(t.pos.x, t.pos.z), {
				pane: "live",
				keyboard: !1,
				icon: k.divIcon({
					className: "ka-zone",
					iconSize: [0, 0],
					html: `<span>⛉ ${X(t.name)}</span>`
				})
			}).addTo(this.zoneLayer), n = (e) => {
				this.stop(e), this.cb.menu({
					kind: "zone",
					name: t.name
				}, this.point(e));
			};
			e.on("click", n), e.on("contextmenu", n);
		}
		t && k.circle(this.toLL(t.pos.x, t.pos.z), {
			radius: t.radius / 2 ** Gi,
			color: "#f9c74f",
			weight: 2,
			dashArray: "2 4",
			fillColor: "#f9c74f",
			fillOpacity: .1,
			pane: "decor",
			renderer: this.decor,
			interactive: !1
		}).addTo(this.zoneLayer);
	}
	setEnemies(e) {
		this.enemyLayer.clearLayers();
		for (let t of e) k.circleMarker(this.toLL(t.pos.x, t.pos.z), {
			renderer: this.icons,
			radius: 5,
			color: "#1a0805",
			weight: 1.5,
			fillColor: "#e0533a",
			fillOpacity: .95
		}).bindTooltip(() => Qi(t.label ?? t.name ?? A("admin.map.enemy"), X(A("admin.map.entity", { entity: t.entity })), `<em>${X(Z(t.pos))}</em>`), {
			direction: "top",
			className: "ka-tip"
		}).addTo(this.enemyLayer);
	}
	routeFrom() {
		let e = this.route;
		if (!e) return null;
		if (e.from.kind === "player") {
			let t = this.players.get(e.from.entity);
			return t ? {
				...t.cur,
				name: t.p.name
			} : null;
		}
		return e.from;
	}
	setRoute(e) {
		this.route = e, e || (this.routeLine?.remove(), this.routeEnd?.remove(), this.routeLine = this.routeEnd = null), this.drawRoute();
	}
	drawRoute() {
		let e = this.route;
		if (!e) return;
		let t = this.routeFrom();
		if (!t) {
			this.routeLine?.remove(), this.routeLine = null;
			return;
		}
		let n = [this.toLL(t.x, t.z), this.toLL(e.to.x, e.to.z)];
		this.routeLine ? this.routeLine.setLatLngs(n) : this.routeLine = k.polyline(n, {
			color: "#f9c74f",
			weight: 3,
			dashArray: "8 8",
			opacity: .95,
			interactive: !1,
			pane: "decor",
			renderer: this.decor
		}).addTo(this.map), this.routeEnd ? this.routeEnd.setLatLng(n[1]) : this.routeEnd = k.circleMarker(n[1], {
			radius: 9,
			color: "#f9c74f",
			weight: 2.5,
			fill: !1,
			interactive: !1,
			pane: "decor",
			renderer: this.decor
		}).addTo(this.map);
	}
}, ra = () => `${yi()}map/`, ia = "kiln.admin.map.v1", aa = {
	vis: null,
	names: !0,
	hideFound: !1,
	found: [],
	finders: [],
	view: null,
	tab: "live",
	side: !0,
	look: {
		spacing: 8,
		fog: !0,
		flame: 5,
		animate: !0
	},
	chestLv: [5, 50],
	surfaceOnly: !1,
	enemies: !1,
	route: null,
	follow: null,
	open: {},
	lang: null,
	foundKey: null
}, oa = () => {
	try {
		return {
			...aa,
			...JSON.parse(localStorage.getItem(ia) ?? "{}")
		};
	} catch {
		return { ...aa };
	}
}, sa = (e) => (e || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase(), ca = (e) => sa(e).split(/[^a-z0-9]+/).filter(Boolean), la = (e, t) => {
	let n = 0;
	for (; n < e.length && n < t.length && e[n] === t[n];) n++;
	return n;
}, ua = (e, t) => t.startsWith(e) || e.length >= 4 && la(e, t) >= Math.max(4, Math.min(e.length, t.length) - 2);
function da(e, ...t) {
	let n = sa(e).trim();
	if (!n) return !0;
	let r = ca(e);
	for (let e of t) {
		if (!e) continue;
		if (sa(e).includes(n)) return !0;
		let t = ca(e);
		if (r.every((e) => t.some((t) => ua(e, t)))) return !0;
	}
	return !1;
}
var fa = (e) => Number(e).toLocaleString(Be() ?? "en-US"), pa = (e) => e >= 1e3 ? A("admin.map.km", { n: (e / 1e3).toFixed(2) }) : A("admin.map.metres", { n: Math.round(e) });
function ma(e, t) {
	switch (Math.round((Math.atan2(t.x - e.x, t.z - e.z) * 180 / Math.PI + 360) % 360 / 45) % 8) {
		case 0: return A("admin.map.dir.n");
		case 1: return A("admin.map.dir.ne");
		case 2: return A("admin.map.dir.e");
		case 3: return A("admin.map.dir.se");
		case 4: return A("admin.map.dir.s");
		case 5: return A("admin.map.dir.sw");
		case 6: return A("admin.map.dir.w");
		default: return A("admin.map.dir.nw");
	}
}
function ha(e, t) {
	switch (e) {
		case "spawns": return A("admin.map.countSpawns", { n: fa(t) });
		case "gather": return A("admin.map.countGather", { n: fa(t) });
		case "ore": return A("admin.map.countOre", { n: fa(t) });
		default: return A("admin.map.countChests", { n: fa(t) });
	}
}
var ga = (e, t) => t.kind === "here" ? A("admin.map.tpItemHere", { who: e }) : t.kind === "grave" ? A("admin.map.tpItemGrave", { who: e }) : A("admin.map.tpItemTo", {
	who: e,
	name: t.name
}), _a = (e, t) => t.kind === "here" ? A("admin.map.moveHere", { who: e }) : t.kind === "grave" ? A("admin.map.moveGrave", { who: e }) : A("admin.map.moveTo", {
	who: e,
	name: t.name
}), va = (e, t, n, r, i) => t.kind === "here" ? A("admin.map.moveAtHere", {
	who: e,
	x: n,
	z: r,
	h: i
}) : t.kind === "grave" ? A("admin.map.moveAtGrave", {
	who: e,
	x: n,
	z: r,
	h: i
}) : A("admin.map.moveAtTo", {
	who: e,
	name: t.name,
	x: n,
	z: r,
	h: i
}), ya = (e, t) => t.kind === "here" ? A("admin.map.teleportedHere", { who: e }) : t.kind === "grave" ? A("admin.map.teleportedGrave", { who: e }) : A("admin.map.teleportedTo", {
	who: e,
	name: t.name
});
function ba({ height: e }) {
	let t = M(), n = Y(), r = i(), a = c(), o = Ft(), s = Jt(n), l = y(() => (o.state?.players ?? []).filter((e) => e.pos), [o.state]), [u, d] = x(oa), f = b(u);
	f.current = u;
	let p = m((e) => d((t) => ({
		...t,
		...e
	})), []);
	g(() => {
		let e = setTimeout(() => {
			try {
				localStorage.setItem(ia, JSON.stringify(u));
			} catch {}
		}, 200);
		return () => clearTimeout(e);
	}, [u]);
	let h = b(null), _ = b(null), [v, S] = x(null), C = Qe().useArtStatus(), ee = `${C?.extracted ?? ""}|${C?.map ?? ""}`, [D, te] = x(null), [O, ne] = x(null), [k, re] = x(!1), [ie, ae] = x(null), [oe, se] = x(""), [ce, le] = x(null), [ue, de] = x(null), [fe, pe] = x(""), [me, he] = x(""), [ge, _e] = x(!1), [ve, ye] = x([]), [be, xe] = x(""), [Se, Ce] = x(null), we = I("zones_list", {}, { refetchInterval: 6e3 }), Te = P("zones_add"), Ee = P("teleport"), De = F("teleport"), Oe = F("teleport_to_player"), ke = (e) => a.setQueriesData({ queryKey: [
		"admin",
		n,
		"zones_list"
	] }, e), Ae = F("zones_add", {
		onSuccess: ke,
		onError: (e) => K(e),
		invalidates: ["gameplay_status"]
	}), je = F("zones_remove", {
		onSuccess: ke,
		onError: (e) => K(e),
		invalidates: ["gameplay_status"]
	}), Me = m(() => {
		de(null), Ce(null);
	}, []);
	g(() => {
		let e = !0;
		return fetch(`${ra()}gpu/d/0_0.webp`, { method: "HEAD" }).then((t) => e && S(t.ok && (t.headers.get("content-type") ?? "").startsWith("image"))).catch(() => e && S(!1)), () => {
			e = !1;
		};
	}, [ee]);
	let Ne = b(() => void 0);
	g(() => {
		if (v === null || !h.current) return;
		let e = new na(h.current, ra(), {
			menu: (e, t) => Ne.current(e, t),
			closeMenu: () => de(null),
			coords: le,
			loading: se,
			view: (e) => p({ view: e }),
			stopFollow: () => {
				p({ follow: null });
			},
			error: (e) => K({ message: e })
		}, {
			look: f.current.look,
			view: f.current.view,
			relief: v
		});
		_.current = e;
		let t = new ResizeObserver(() => {
			e.map.invalidateSize(), e.game?.redraw();
		});
		return t.observe(h.current), v && e.loadMarkers(f.current.lang).then((t) => {
			te(t), ae(e.lang ?? null);
			let n = f.current, r = { ...n.vis ?? {} };
			for (let e of t.cats) e.id in r || (r[e.id] = e.on);
			let i = qi(t), a = n.foundKey === i || n.foundKey == null && i === "copy";
			p({
				vis: r,
				foundKey: i,
				...a ? {} : { found: [] }
			}), e.loadFind().then((e) => {
				ne(e), re(!1);
			}, () => re(!0));
		}).catch((e) => K({ message: A("admin.map.mapData", { message: e.message }) })), () => {
			t.disconnect(), e.destroy(), _.current = null;
		};
	}, [v]), g(() => {
		_.current?.map.invalidateSize();
	}, [e, u.side]);
	let Pe = m((e) => {
		let t = _.current;
		t && t.setLang(e).then((t) => {
			if (!t) return K({ message: A("admin.map.noMarkerTexts", { code: e }) });
			p({ lang: e }), ae(e), te((e) => e && { ...e });
		}, (e) => K({ message: A("admin.map.markerTexts", { message: e.message }) }));
	}, [p]);
	g(() => {
		D && u.vis && _.current?.applyMarkers(u.vis, u.names, u.found, u.hideFound, u.chestLv);
	}, [
		D,
		u.vis,
		u.names,
		u.found,
		u.hideFound,
		u.chestLv
	]), g(() => {
		O && _.current?.setFinders(u.finders, u.surfaceOnly);
	}, [
		O,
		u.finders,
		u.surfaceOnly,
		u.chestLv
	]), g(() => {
		_.current?.setLook(u.look);
	}, [u.look]), g(() => {
		_.current?.setPlayers(l);
	}, [l, v]), g(() => {
		_.current && (_.current.follow = u.follow);
	}, [u.follow, v]), g(() => {
		_.current?.setWorld(s.waypoints, s.graves);
	}, [
		s.waypoints,
		s.graves,
		v
	]), g(() => {
		_.current?.setZones(we.data ?? [], Se);
	}, [
		we.data,
		Se,
		v
	]), g(() => {
		_.current?.setRoute(u.route);
	}, [u.route, v]), g(() => {
		_.current?.setEnemies(u.enemies ? ve.map((e) => ({
			...e,
			label: s.enemyLabels[e.guid ?? ""] || xi(e.name)
		})) : []);
	}, [
		ve,
		u.enemies,
		s.enemyLabels,
		v
	]);
	let Fe = ki();
	g(() => {
		Fe.focus && _.current && (_.current.flyTo(Fe.focus.x, Fe.focus.z, Fe.focus.zoom ?? 5.5), Fe.set(null));
	}, [Fe, v]);
	let Ie = P("enemies");
	g(() => {
		if (!u.enemies || !Ie) return;
		let e = !1, t = async () => {
			let t = /* @__PURE__ */ new Map(), r = "";
			for (let e of l) try {
				let r = await kt(n, "enemies", {
					near: {
						x: e.pos.x,
						y: e.pos.y,
						z: e.pos.z
					},
					radius: 80
				});
				for (let e of r.near) e.pos && t.set(e.entity, e);
			} catch (e) {
				r = e.message;
			}
			e || (ye([...t.values()]), xe(r));
		};
		t();
		let r = setInterval(t, 5e3);
		return () => {
			e = !0, clearInterval(r);
		};
	}, [
		u.enemies,
		Ie,
		l.length,
		n
	]);
	let Le = async (e, t) => {
		if (e.y != null) return e.y;
		let n = await Ui.exact(e.x, e.z);
		return n == null ? null : n + t;
	}, Re = async (e, n, r) => {
		let i = J(e);
		if ("player" in n) return await q({
			title: t("admin.map.teleportTitle", { who: i }),
			body: /* @__PURE__ */ T("p", { children: _a(i, r) }),
			ok: t("admin.map.teleport")
		}) ? Oe.mutate({
			entity: e.entity,
			target: n.player
		}, {
			onSuccess: () => G(ya(i, r)),
			onError: (e) => K(e)
		}) : void 0;
		let a = await Le(n, 3);
		if (a == null) return K({ message: t("admin.map.noHeightRelief") });
		let o = {
			x: +n.x.toFixed(2),
			y: +a.toFixed(2),
			z: +n.z.toFixed(2)
		};
		await q({
			title: t("admin.map.teleportTitle", { who: i }),
			body: /* @__PURE__ */ T("p", { children: va(i, r, Math.round(o.x), Math.round(o.z), Math.round(o.y)) }),
			ok: t("admin.map.teleport")
		}) && De.mutate({
			entity: e.entity,
			...o,
			safe: !0
		}, {
			onSuccess: () => G(ya(i, r)),
			onError: (e) => K(e)
		});
	}, ze = (e) => p({ route: e }), Be = (e) => {
		let n = l.map((n) => ({
			t: t("admin.map.routeFromHere", { from: J(n) }),
			fn: () => ze({
				from: {
					kind: "player",
					entity: n.entity,
					name: n.name
				},
				to: e
			})
		})), r = f.current.route;
		return r && r.from.kind === "point" && n.push({
			t: r.from.name ? t("admin.map.routeFromHere", { from: r.from.name }) : t("admin.map.routeFromMarked"),
			fn: () => ze({
				from: r.from,
				to: e
			})
		}), n.push({
			t: t("admin.map.measureFromHere"),
			fn: () => {
				ze({
					from: {
						kind: "point",
						x: e.x,
						y: e.y ?? 0,
						z: e.z,
						name: e.name
					},
					to: e
				}), G(t("admin.map.startSet"), t("admin.map.startSetDetail"));
			}
		}), n;
	}, Ve = (e, t, n) => Ee ? l.filter((e) => e.entity !== n).map((n) => ({
		t: ga(J(n), t),
		fn: () => Re(n, e, t)
	})) : [], He = (e, r, i) => {
		de({
			title: t("admin.map.saveWaypoint"),
			sub: Z(e),
			point: i,
			items: [],
			form: /* @__PURE__ */ T(Sa, {
				initial: r,
				onCancel: Me,
				onSave: async (r) => {
					let i = await Le(e, 1);
					if (i == null) return K({ message: t("admin.map.noHeight") });
					let a = $t(n, r, {
						x: e.x,
						y: i,
						z: e.z
					});
					if (a) return K({ message: a });
					G(t("admin.players.savedWaypoint", { name: r })), Me();
				}
			})
		});
	}, Ue = (e, n, i = "") => {
		Ce({
			pos: e,
			radius: 30
		}), de({
			title: t("admin.map.protectHere"),
			sub: Z(e),
			point: n,
			items: [],
			form: /* @__PURE__ */ T(Ca, {
				initial: i,
				onRadius: (t) => Ce({
					pos: e,
					radius: t
				}),
				onCancel: Me,
				onSave: async (n) => {
					let r = await Le(e, 0), i = Di({
						...n,
						x: String(e.x),
						y: String(r ?? 0),
						z: String(e.z),
						owners: [],
						allowAdmins: !0,
						editing: !1
					});
					if (i.error) return K({ message: i.error });
					Ae.mutate(i.args, { onSuccess: () => {
						G(t("admin.zones.added", { name: n.name }), t("admin.map.zoneAddedDetail")), Me();
					} });
				},
				onMore: async () => {
					let t = await Le(e, 0);
					Oi.getState().set({
						x: e.x,
						y: t ?? 0,
						z: e.z
					}), r("../zones");
				}
			})
		});
	};
	Ne.current = (e, i) => {
		if (e.kind === "spot") {
			let n = e.pos, r = {
				...n,
				name: `${Math.round(n.x)}, ${Math.round(n.z)}`
			}, a = (e) => e == null ? t("admin.map.posXZ", {
				x: Math.round(n.x),
				z: Math.round(n.z)
			}) : t("admin.map.spotSubGround", {
				x: Math.round(n.x),
				z: Math.round(n.z),
				h: Math.round(e)
			});
			de({
				title: t("admin.map.thisSpot"),
				spot: !0,
				sub: a(n.y),
				point: i,
				items: [
					{
						t: t("admin.map.saveWaypointHere"),
						keep: !0,
						fn: () => He(n, "", i)
					},
					...Te ? [{
						t: t("admin.map.protectHereMenu"),
						keep: !0,
						fn: () => Ue(n, i)
					}] : [],
					...Ve(n, { kind: "here" }),
					{
						sep: !0,
						t: ""
					},
					...Be(r),
					{
						t: t("admin.map.copyCoords"),
						fn: () => navigator.clipboard?.writeText(`X ${n.x} Z ${n.z}${n.y == null ? "" : ` H ${Math.round(n.y)}`}`).then(() => G(t("admin.map.coordsCopied")))
					}
				]
			}), Ui.exact(n.x, n.z).then((e) => {
				e != null && de((t) => t && t.spot && t.point === i ? {
					...t,
					sub: a(e)
				} : t);
			});
		} else if (e.kind === "marker") {
			let n = e.pos, r = [
				...Be({
					...n,
					name: e.name
				}),
				{
					sep: !0,
					t: ""
				},
				{
					t: t("admin.map.saveWaypointHere"),
					keep: !0,
					fn: () => He(n, e.name.slice(0, 40), i)
				},
				...Te ? [{
					t: t("admin.map.protectHereMenu"),
					keep: !0,
					fn: () => Ue(n, i, e.name.slice(0, 64))
				}] : [],
				...Ve(n, {
					kind: "to",
					name: e.name
				})
			];
			if (e.idx != null) {
				let n = f.current.found.includes(e.idx);
				r.push({
					sep: !0,
					t: ""
				}, {
					t: t(n ? "admin.map.unmarkFound" : "admin.map.markFound"),
					fn: () => p({ found: n ? f.current.found.filter((t) => t !== e.idx) : [...f.current.found, e.idx] })
				});
			}
			de({
				title: e.name,
				sub: `${e.sub ? e.sub + " · " : ""}${Z(n)}`,
				point: i,
				items: r
			});
		} else if (e.kind === "player") {
			let n = l.find((t) => t.entity === e.entity);
			if (!n?.pos) return;
			let a = J(n), o = l.filter((t) => t.entity !== e.entity);
			de({
				title: a,
				sub: n.healthMax ? t("admin.map.playerSubHealth", {
					pos: Z(n.pos),
					health: Math.round(n.health ?? 0),
					max: Math.round(n.healthMax)
				}) : Z(n.pos),
				point: i,
				items: [
					{
						t: f.current.follow === e.entity ? t("admin.map.stopFollowing") : t("admin.map.followOnMap"),
						fn: () => p({ follow: f.current.follow === e.entity ? null : e.entity })
					},
					{
						t: t("admin.map.routeFromTo", { from: a }),
						keep: !0,
						fn: () => {
							ze({
								from: {
									kind: "player",
									entity: e.entity,
									name: a
								},
								to: f.current.route?.to ?? {
									...n.pos,
									name: a
								}
							}), G(t("admin.map.clickDestination"));
						}
					},
					...o.map((e) => ({
						t: t("admin.map.routeFromToName", {
							from: J(e),
							to: a
						}),
						fn: () => ze({
							from: {
								kind: "player",
								entity: e.entity,
								name: e.name
							},
							to: {
								...n.pos,
								name: a
							}
						})
					})),
					{
						t: t("admin.map.saveWaypointAt", { name: a }),
						keep: !0,
						fn: () => He(n.pos, t("admin.players.spotName", { name: a }).slice(0, 40), i)
					},
					...Ve({ player: e.entity }, {
						kind: "to",
						name: a
					}, e.entity),
					{
						sep: !0,
						t: ""
					},
					{
						t: t("admin.map.openPlayers"),
						fn: () => r("../players")
					}
				]
			});
		} else if (e.kind === "waypoint") de({
			title: e.name,
			sub: t("admin.map.waypointSub", { pos: Z(e.pos) }),
			point: i,
			items: [
				...Be({
					...e.pos,
					name: e.name
				}),
				...Ve(e.pos, {
					kind: "to",
					name: e.name
				}),
				{
					sep: !0,
					t: ""
				},
				{
					t: t("admin.map.deleteWaypoint"),
					fn: async () => {
						await q({
							title: t("admin.map.deleteWaypointTitle"),
							body: /* @__PURE__ */ T("p", { children: e.name }),
							ok: t("common.delete"),
							danger: !0
						}) && en(n, e.name);
					}
				}
			]
		});
		else if (e.kind === "grave") {
			let n = e.name == null ? t("admin.map.graveSomeone") : t("admin.map.graveOf", { name: e.name });
			de({
				title: n,
				sub: `${Fr(e.at)} · ${Z(e.pos)}`,
				point: i,
				items: [
					...Be({
						...e.pos,
						name: n
					}),
					...Ve({
						...e.pos,
						y: e.pos.y + 1
					}, { kind: "grave" }),
					{
						t: t("admin.map.saveWaypointHere"),
						keep: !0,
						fn: () => He(e.pos, (e.name == null ? t("admin.map.graveWaypointNone") : t("admin.map.graveWaypoint", { name: e.name })).slice(0, 40), i)
					}
				]
			});
		} else if (e.kind === "zone") {
			let n = (we.data ?? []).find((t) => t.name === e.name);
			if (!n) return;
			de({
				title: `⛉ ${n.name}`,
				sub: t("admin.map.zoneSub", {
					radius: Math.round(n.radius),
					deny: n.deny.map((e) => wi(e)).join(", ")
				}),
				point: i,
				items: [
					{
						t: t("admin.map.changeRadius"),
						keep: !0,
						fn: () => {
							Ce({
								pos: n.pos,
								radius: n.radius
							}), de({
								title: t("admin.map.radiusOf", { name: n.name }),
								sub: Z(n.pos),
								point: i,
								items: [],
								form: /* @__PURE__ */ T(wa, {
									initial: n.radius,
									onRadius: (e) => Ce({
										pos: n.pos,
										radius: e
									}),
									onCancel: Me,
									onSave: (e) => Ae.mutate({
										name: n.name,
										x: n.pos.x,
										y: n.pos.y,
										z: n.pos.z,
										radius: e,
										deny: n.deny,
										owners: n.owners,
										allowAdmins: n.allowAdmins
									}, { onSuccess: () => {
										G(t("admin.map.zoneRadiusSet", {
											name: n.name,
											radius: e
										})), Me();
									} })
								})
							});
						}
					},
					{
						t: t("admin.map.editOnZones"),
						fn: () => r("../zones")
					},
					...Be({
						...n.pos,
						name: n.name
					}),
					{
						sep: !0,
						t: ""
					},
					{
						t: t("admin.map.removeZone"),
						fn: async () => {
							await q({
								title: t("admin.map.removeZoneTitle", { name: n.name }),
								body: /* @__PURE__ */ T("p", { children: t("admin.zones.removeBody") }),
								ok: t("common.remove"),
								danger: !0
							}) && je.mutate({ name: n.name }, { onSuccess: () => G(t("admin.zones.removed", { name: n.name })) });
						}
					}
				]
			});
		}
	};
	let We = (e, t) => u.finders.some((n) => n.kind === e && n.slug === t), Ge = (e, t) => p({ finders: We(e, t) ? u.finders.filter((n) => n.kind !== e || n.slug !== t) : [...u.finders, {
		kind: e,
		slug: t
	}] }), Ke = (e) => p({ finders: [...u.finders, ...e.filter(([e, t]) => !We(e, t)).map(([e, t]) => ({
		kind: e,
		slug: t
	}))] }), qe = y(() => {
		let e = [], n = fe.trim();
		if (n.length < 2) return e;
		let r = (t, n) => {
			n.length && e.push({
				sec: t,
				items: n
			});
		};
		if (O && n.length >= 3 && r(t("admin.map.secDropped"), Object.entries(O.drops).filter(([e]) => da(n, e)).slice(0, 5).map(([e, n]) => ({
			img: n.img,
			t: e,
			s: t("admin.map.droppedBy", { list: `${n.by.slice(0, 4).map((e) => O.spawns[e]?.n ?? e).join(", ")}${n.by.length > 4 ? ` +${n.by.length - 4}` : ""}` }),
			act: () => {
				Ke(n.by.filter((e) => O.spawns[e]).map((e) => ["spawns", e])), G(t("admin.map.showingDrops", {
					count: n.by.length,
					item: e
				}));
			}
		}))), O) {
			let e = [];
			for (let t of [
				"spawns",
				"gather",
				"ore",
				"chests"
			]) for (let [r, i] of Object.entries(O[t])) da(n, i.n) && e.push({
				...i,
				kind: t,
				slug: r
			});
			e.sort((e, t) => Number(!!t.boss) - Number(!!e.boss) || t.c - e.c), r(t("admin.map.secWhere"), e.slice(0, 8).map((e) => ({
				img: e.img,
				color: e.color || Ji[e.kind],
				t: e.n,
				on: We(e.kind, e.slug),
				s: (() => {
					let n = `${e.kind === "chests" ? t("admin.map.chestsHold", { n: fa(e.c) }) : ha(e.kind, e.c)}${e.boss ? t("admin.map.bossTag") : ""}`;
					return We(e.kind, e.slug) ? t("admin.map.hitOn", { what: n }) : t("admin.map.hitOff", { what: n });
				})(),
				act: () => Ge(e.kind, e.slug)
			})));
		}
		let i = [];
		for (let e of l) da(n, e.name) && i.push({
			t: J(e),
			s: t("admin.map.playerSub", { pos: Z(e.pos) }),
			color: "#7dff9b",
			act: () => _.current?.flyTo(e.pos.x, e.pos.z, 5)
		});
		for (let e of s.waypoints) da(n, e.name) && i.push({
			t: e.name,
			s: t("admin.map.waypointSub", { pos: Z(e.pos) }),
			img: "g-mapmarker_waypoint.webp",
			act: () => _.current?.flyTo(e.pos.x, e.pos.z, 5)
		});
		for (let e of we.data ?? []) da(n, e.name) && i.push({
			t: e.name,
			s: t("admin.map.zoneSearchSub", { radius: Math.round(e.radius) }),
			color: "#ff8540",
			act: () => _.current?.flyTo(e.pos.x, e.pos.z, 5)
		});
		if (r(t("admin.map.secLive"), i.slice(0, 8)), D) {
			r(t("admin.map.secPlaces"), D.labels.filter((e) => da(n, e[0])).slice(0, 6).map((e) => ({
				t: e[0],
				s: t("admin.map.placeSub", { pos: Z({
					x: e[1],
					z: e[2]
				}) }),
				color: "#d8c9a6",
				act: () => _.current?.flyTo(e[1], e[2], 5)
			})));
			let e = [];
			for (let t = 0; t < D.points.length && e.length < 10; t++) {
				let r = D.points[t], i = D.cats[r[0]];
				da(n, r[4], i.label) && e.push({
					img: r[7] || i.icon,
					t: r[4],
					s: `${Zi(i.label, r[6])} · ${Z({
						x: r[1],
						z: r[2]
					})}`,
					act: () => {
						u.vis?.[i.id] || p({ vis: {
							...u.vis ?? {},
							[i.id]: !0
						} }), _.current?.flyTo(r[1], r[2], 5.5);
					}
				});
			}
			r(t("admin.map.secMarkers"), e);
		}
		return e;
	}, [
		fe,
		O,
		D,
		l,
		s.waypoints,
		we.data,
		u.finders,
		u.vis,
		t
	]), N = u.route, Je = N ? N.from.kind === "player" ? (() => {
		let e = l.find((e) => e.entity === N.from.entity);
		return e?.pos ? {
			...e.pos,
			name: J(e)
		} : null;
	})() : N.from : null;
	return /* @__PURE__ */ E("div", {
		className: "ka-map-root" + (u.side ? "" : " noside"),
		style: { height: e },
		onKeyDown: (e) => {
			e.key === "Escape" && (Me(), _e(!1));
		},
		children: [/* @__PURE__ */ E("aside", {
			className: "ka-map-side",
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-map-search",
					children: [/* @__PURE__ */ T(R, {
						type: "search",
						"aria-label": t("admin.map.searchLabel"),
						placeholder: t("admin.map.searchPlaceholder"),
						value: fe,
						autoComplete: "off",
						spellCheck: !1,
						onChange: (e) => {
							pe(e.target.value), _e(!0);
						},
						onFocus: () => _e(!0),
						onKeyDown: (e) => {
							e.key === "Enter" && qe[0]?.items[0] && (qe[0].items[0].act(), _e(!1));
						}
					}), ge && fe.trim().length >= 2 ? /* @__PURE__ */ T("div", {
						className: "ka-map-results",
						onMouseLeave: () => void 0,
						children: qe.length ? qe.map((e) => /* @__PURE__ */ E("div", { children: [/* @__PURE__ */ T("h4", { children: e.sec }), e.items.map((e, t) => /* @__PURE__ */ E("button", {
							type: "button",
							className: e.on ? "on" : "",
							onClick: () => {
								e.act(), _e(!1);
							},
							children: [
								e.img ? /* @__PURE__ */ T("img", {
									src: `${ra()}i/${e.img}`,
									alt: ""
								}) : /* @__PURE__ */ T("i", {
									className: "ka-dot",
									style: { background: e.color ?? "#d8c9a6" }
								}),
								/* @__PURE__ */ T("b", { children: e.t }),
								/* @__PURE__ */ T("small", { children: e.s })
							]
						}, t))] }, e.sec)) : /* @__PURE__ */ T("p", {
							className: "ka-map-muted",
							children: t("admin.map.nothingMatches")
						})
					}) : null]
				}),
				/* @__PURE__ */ T("div", {
					className: "ka-map-tabs",
					role: "tablist",
					children: [
						["layers", t("admin.map.tabMarkers")],
						["find", t("admin.map.tabFind")],
						["live", t("admin.map.tabLive")]
					].map(([e, t]) => /* @__PURE__ */ T("button", {
						role: "tab",
						className: u.tab === e ? "on" : "",
						onClick: () => p({ tab: e }),
						children: t
					}, e))
				}),
				/* @__PURE__ */ E("div", {
					className: "ka-map-panel",
					onClick: () => _e(!1),
					children: [
						u.tab === "layers" ? /* @__PURE__ */ T(Ta, {
							D,
							ui: u,
							setUi: p,
							assets: v,
							lang: ie,
							onLang: Pe
						}) : null,
						u.tab === "find" ? /* @__PURE__ */ T(Ea, {
							F: O,
							missing: k,
							ui: u,
							findQ: me,
							setFindQ: he,
							toggle: Ge,
							addAll: Ke,
							dropsBy: _.current?.dropsBy ?? {},
							clear: () => p({ finders: [] }),
							setUi: p
						}) : null,
						u.tab === "live" ? /* @__PURE__ */ T(Da, {
							players: l,
							connected: o.ready,
							reason: o.error?.message,
							waypoints: s.waypoints,
							graves: s.graves,
							zones: we.data ?? [],
							ui: u,
							setUi: p,
							enemies: ve.length,
							enemyErr: be,
							hasEnemies: Ie,
							routeFrom: Je?.name ?? (N?.from.kind === "point" ? N.from.name : void 0),
							go: (e, t) => _.current?.flyTo(e, t, 5),
							follow: (e) => {
								let t = u.follow === e ? null : e;
								p({ follow: t });
								let n = t == null ? null : l.find((e) => e.entity === t)?.pos;
								n && _.current?.flyTo(n.x, n.z, 5);
							}
						}) : null
					]
				}),
				/* @__PURE__ */ T("div", {
					className: "ka-map-credit",
					children: j("admin.map.credit", { map: /* @__PURE__ */ T("b", { children: t("admin.map.creditMap") }) })
				})
			]
		}), /* @__PURE__ */ E("div", {
			className: "ka-map-area",
			children: [
				/* @__PURE__ */ T("div", {
					className: "ka-map",
					ref: h
				}),
				v === !1 ? /* @__PURE__ */ T("div", {
					style: {
						position: "absolute",
						left: 56,
						right: 56,
						top: 12,
						zIndex: 1001
					},
					children: /* @__PURE__ */ T(H, {
						tone: "info",
						title: C?.canExtract === !1 ? t("admin.map.artNeedsClient") : t("admin.map.artPreparing")
					})
				}) : null,
				/* @__PURE__ */ T("button", {
					className: "ka-map-sidetoggle",
					title: t("admin.map.toggleSide"),
					onClick: () => p({ side: !u.side }),
					children: "☰"
				}),
				N ? /* @__PURE__ */ E("div", {
					className: "ka-map-route",
					children: [Je ? /* @__PURE__ */ E(w, { children: [
						/* @__PURE__ */ E("span", { children: [
							/* @__PURE__ */ T("b", { children: Je.name ?? t("admin.map.start") }),
							" → ",
							/* @__PURE__ */ T("b", { children: N.to.name ?? t("admin.map.point") })
						] }),
						/* @__PURE__ */ T("span", {
							className: "big",
							children: pa(Math.hypot(N.to.x - Je.x, N.to.z - Je.z))
						}),
						/* @__PURE__ */ T("span", { children: ma(Je, N.to) }),
						N.to.y != null && Je.y != null ? (() => {
							let e = N.to.y - Je.y;
							return /* @__PURE__ */ E("span", {
								className: e > 2 ? "up" : e < -2 ? "down" : "",
								children: [
									e > 2 ? "▲" : e < -2 ? "▼" : "≈",
									" ",
									t("admin.map.metres", { n: Math.abs(Math.round(e)) })
								]
							});
						})() : null
					] }) : /* @__PURE__ */ T("span", { children: t("admin.map.routeStartGone", { start: N.from.kind === "player" ? N.from.name ?? t("admin.map.player") : t("admin.map.point") }) }), /* @__PURE__ */ T("button", {
						className: "ka-map-x",
						title: t("admin.map.clearRouteTip"),
						onClick: () => ze(null),
						children: "✕"
					})]
				}) : null,
				/* @__PURE__ */ E("div", {
					className: "ka-map-zoom",
					children: [
						/* @__PURE__ */ T("button", {
							title: t("admin.map.zoomIn"),
							onClick: () => _.current?.map.zoomIn(),
							children: "+"
						}),
						/* @__PURE__ */ T("button", {
							title: t("admin.map.zoomOut"),
							onClick: () => _.current?.map.zoomOut(),
							children: "−"
						}),
						/* @__PURE__ */ T("button", {
							title: t("admin.map.wholeWorld"),
							onClick: () => _.current?.fit(),
							children: "⤢"
						})
					]
				}),
				ce ? /* @__PURE__ */ E("div", {
					className: "ka-map-coords",
					children: [
						/* @__PURE__ */ E("span", { children: ["X ", /* @__PURE__ */ T("b", { children: Math.round(ce.x) })] }),
						/* @__PURE__ */ E("span", { children: ["Z ", /* @__PURE__ */ T("b", { children: Math.round(ce.z) })] }),
						ce.y == null ? null : /* @__PURE__ */ T("span", { children: j("admin.map.heightReadout", { h: /* @__PURE__ */ E("b", { children: ["≈", Math.round(ce.y)] }) }) })
					]
				}) : null,
				oe ? /* @__PURE__ */ T("div", {
					className: "ka-map-loading",
					children: oe
				}) : null,
				ue ? /* @__PURE__ */ T(xa, {
					menu: ue,
					close: Me
				}) : null
			]
		})]
	});
}
function xa({ menu: e, close: t }) {
	let n = b(null), [r, i] = x({
		left: e.point.x + 6,
		top: e.point.y + 6
	});
	return g(() => {
		let t = n.current, r = t?.parentElement;
		t && r && i({
			left: Math.max(8, Math.min(e.point.x + 6, r.clientWidth - t.offsetWidth - 8)),
			top: Math.max(8, Math.min(e.point.y + 6, r.clientHeight - t.offsetHeight - 8))
		});
	}, [e]), /* @__PURE__ */ E("div", {
		ref: n,
		className: "ka-map-menu",
		style: r,
		onContextMenu: (e) => e.preventDefault(),
		role: "menu",
		children: [
			/* @__PURE__ */ E("div", {
				className: "ka-map-mh",
				children: [/* @__PURE__ */ T("b", { children: e.title }), e.sub ? /* @__PURE__ */ T("small", { children: e.sub }) : null]
			}),
			e.items.map((e, n) => e.sep ? /* @__PURE__ */ T("hr", {}, n) : /* @__PURE__ */ E("button", {
				role: "menuitem",
				disabled: e.disabled,
				onClick: () => {
					e.keep || t(), e.fn?.();
				},
				children: [e.t, e.tag ? /* @__PURE__ */ T("span", {
					className: "tag",
					children: e.tag
				}) : null]
			}, n)),
			e.form
		]
	});
}
function Sa({ initial: e, onSave: t, onCancel: n }) {
	let r = M(), [i, a] = x(e);
	return /* @__PURE__ */ T("form", {
		className: "ka-map-form",
		onSubmit: (e) => {
			e.preventDefault(), i.trim() && t(i.trim());
		},
		onKeyDown: (e) => {
			e.key === "Escape" && n(), e.stopPropagation();
		},
		children: /* @__PURE__ */ E("div", {
			className: "ka-inline",
			children: [/* @__PURE__ */ T(R, {
				autoFocus: !0,
				maxLength: 40,
				placeholder: r("admin.map.namePlaceholder"),
				value: i,
				onChange: (e) => a(e.target.value)
			}), /* @__PURE__ */ T(L, {
				size: "s",
				variant: "primary",
				type: "submit",
				children: r("common.save")
			})]
		})
	});
}
function Ca({ initial: e, onSave: t, onCancel: n, onRadius: r, onMore: i }) {
	let a = M(), [o, s] = x(e), [c, l] = x("30"), [u, d] = x(Ci);
	return /* @__PURE__ */ E("form", {
		className: "ka-map-form",
		onSubmit: (e) => {
			e.preventDefault(), t({
				name: o,
				radius: c,
				deny: u
			});
		},
		onKeyDown: (e) => {
			e.key === "Escape" && n(), e.stopPropagation();
		},
		children: [
			/* @__PURE__ */ T(R, {
				autoFocus: !0,
				maxLength: 64,
				placeholder: a("admin.map.zoneNamePlaceholder"),
				value: o,
				onChange: (e) => s(e.target.value)
			}),
			/* @__PURE__ */ E("div", {
				className: "ka-inline",
				children: [
					/* @__PURE__ */ T("span", {
						className: "k-hint",
						children: a("admin.zones.colRadius")
					}),
					/* @__PURE__ */ T(R, {
						inputMode: "numeric",
						value: c,
						onChange: (e) => {
							l(e.target.value);
							let t = Number(e.target.value);
							t >= 1 && t <= 2e3 && r(t);
						},
						style: { width: 80 }
					}),
					/* @__PURE__ */ T("span", {
						className: "k-hint",
						children: a("admin.map.metreUnit")
					})
				]
			}),
			/* @__PURE__ */ T("div", {
				className: "ka-flags",
				children: Si.map((e) => /* @__PURE__ */ E("label", { children: [
					/* @__PURE__ */ T("input", {
						type: "checkbox",
						checked: u.includes(e),
						onChange: (t) => d(t.target.checked ? [...u, e] : u.filter((t) => t !== e))
					}),
					" ",
					wi(e)
				] }, e))
			}),
			/* @__PURE__ */ E("div", {
				className: "ka-inline",
				children: [/* @__PURE__ */ T(L, {
					size: "s",
					variant: "primary",
					type: "submit",
					children: a("admin.zones.addZone")
				}), /* @__PURE__ */ T(L, {
					size: "s",
					variant: "ghost",
					onClick: i,
					children: a("admin.map.ownersMore")
				})]
			})
		]
	});
}
function wa({ initial: e, onSave: t, onCancel: n, onRadius: r }) {
	let i = M(), [a, o] = x(String(Math.round(e))), s = Number(a);
	return /* @__PURE__ */ E("form", {
		className: "ka-map-form",
		onSubmit: (e) => {
			e.preventDefault(), s >= 1 && s <= 2e3 ? t(s) : K({ message: i("admin.zones.radiusRange") });
		},
		onKeyDown: (e) => {
			e.key === "Escape" && n(), e.stopPropagation();
		},
		children: [/* @__PURE__ */ T("input", {
			type: "range",
			min: 1,
			max: 500,
			value: Math.min(500, s || 1),
			onChange: (e) => {
				o(e.target.value), r(Number(e.target.value));
			},
			className: "ka-dial"
		}), /* @__PURE__ */ E("div", {
			className: "ka-inline",
			children: [
				/* @__PURE__ */ T(R, {
					autoFocus: !0,
					inputMode: "numeric",
					value: a,
					onChange: (e) => {
						o(e.target.value);
						let t = Number(e.target.value);
						t >= 1 && t <= 2e3 && r(t);
					},
					style: { width: 90 }
				}),
				/* @__PURE__ */ T("span", {
					className: "k-hint",
					children: i("admin.map.metreUnit")
				}),
				/* @__PURE__ */ T(L, {
					size: "s",
					variant: "primary",
					type: "submit",
					children: i("common.save")
				})
			]
		})]
	});
}
function Ta({ D: e, ui: t, setUi: n, assets: r, lang: i, onLang: a }) {
	let o = M();
	if (r === !1) return /* @__PURE__ */ T("p", {
		className: "ka-map-muted",
		children: o("admin.map.noMapData")
	});
	if (!e || !t.vis) return /* @__PURE__ */ T("p", {
		className: "ka-map-muted",
		children: o("admin.map.loadingMarkers")
	});
	let s = t.vis, c = [];
	for (let t of e.cats) {
		let e = c.find((e) => e.name === t.group);
		e || c.push(e = {
			name: t.group,
			cats: []
		}), e.cats.push(t);
	}
	let l = [
		5,
		10,
		13,
		15,
		20,
		27,
		30,
		35,
		40,
		45,
		50
	], u = e.points.filter((t) => s[e.cats[t[0]].id]).length, d = (e) => n({ look: {
		...t.look,
		...e
	} });
	return /* @__PURE__ */ E(w, { children: [
		/* @__PURE__ */ E("div", {
			className: "ka-map-tools",
			children: [
				/* @__PURE__ */ T(L, {
					size: "s",
					variant: "secondary",
					onClick: () => n({ vis: Object.fromEntries(e.cats.map((e) => [e.id, !0])) }),
					children: o("admin.map.showAll")
				}),
				/* @__PURE__ */ T(L, {
					size: "s",
					variant: "secondary",
					onClick: () => n({ vis: Object.fromEntries(e.cats.map((e) => [e.id, !1])) }),
					children: o("admin.map.hideAll")
				}),
				/* @__PURE__ */ E("label", { children: [
					/* @__PURE__ */ T("input", {
						type: "checkbox",
						checked: t.names,
						onChange: (e) => n({ names: e.target.checked })
					}),
					" ",
					o("admin.map.placeNames")
				] }),
				/* @__PURE__ */ E("label", { children: [
					/* @__PURE__ */ T("input", {
						type: "checkbox",
						checked: t.hideFound,
						onChange: (e) => n({ hideFound: e.target.checked })
					}),
					" ",
					o("admin.map.hideFound")
				] }),
				/* @__PURE__ */ E("label", { children: [
					o("admin.map.chestLevel"),
					" ",
					/* @__PURE__ */ T("select", {
						value: t.chestLv[0],
						onChange: (e) => {
							let r = +e.target.value;
							n({ chestLv: [r, Math.max(r, t.chestLv[1])] });
						},
						children: l.map((e) => /* @__PURE__ */ T("option", { children: e }, e))
					}),
					o("admin.map.chestLevelTo"),
					" ",
					/* @__PURE__ */ T("select", {
						value: t.chestLv[1],
						onChange: (e) => {
							let r = +e.target.value;
							n({ chestLv: [Math.min(t.chestLv[0], r), r] });
						},
						children: l.map((e) => /* @__PURE__ */ T("option", { children: e }, e))
					})
				] })
			]
		}),
		c.map((e) => {
			let r = t.open[e.name] ?? !0, i = e.cats.filter((e) => s[e.id]).length;
			return /* @__PURE__ */ E("section", {
				className: "ka-map-group",
				children: [/* @__PURE__ */ E("h3", { children: [/* @__PURE__ */ T("span", {
					onClick: () => {
						let t = e.cats.some((e) => !s[e.id]);
						n({ vis: {
							...s,
							...Object.fromEntries(e.cats.map((e) => [e.id, t]))
						} });
					},
					title: o("admin.map.toggleGroup"),
					children: e.name
				}), /* @__PURE__ */ E("small", {
					onClick: () => n({ open: {
						...t.open,
						[e.name]: !r
					} }),
					children: [
						i,
						"/",
						e.cats.length,
						" ",
						r ? "▾" : "▸"
					]
				})] }), r ? /* @__PURE__ */ T("div", {
					className: "ka-map-cats",
					children: e.cats.map((e) => /* @__PURE__ */ E("button", {
						type: "button",
						className: "ka-map-cat" + (s[e.id] ? "" : " off"),
						title: e.label,
						onClick: () => n({ vis: {
							...s,
							[e.id]: !s[e.id]
						} }),
						children: [
							/* @__PURE__ */ T("img", {
								src: `${ra()}i/${e.icon}`,
								alt: ""
							}),
							/* @__PURE__ */ T("span", { children: e.label }),
							/* @__PURE__ */ T("small", { children: fa(e.count) })
						]
					}, e.id))
				}) : null]
			}, e.name);
		}),
		/* @__PURE__ */ E("section", {
			className: "ka-map-group ka-map-look",
			children: [
				/* @__PURE__ */ T("h3", { children: /* @__PURE__ */ T("span", { children: o("admin.map.mapLook") }) }),
				/* @__PURE__ */ E("label", { children: [
					o("admin.map.contourEvery"),
					" ",
					/* @__PURE__ */ T("select", {
						value: t.look.spacing,
						onChange: (e) => d({ spacing: +e.target.value }),
						children: [
							4,
							8,
							16,
							32
						].map((e) => /* @__PURE__ */ T("option", {
							value: e,
							children: o("admin.map.metres", { n: e })
						}, e))
					})
				] }),
				/* @__PURE__ */ E("label", { children: [
					/* @__PURE__ */ T("input", {
						type: "checkbox",
						checked: t.look.fog,
						onChange: (e) => d({ fog: e.target.checked })
					}),
					" ",
					o("admin.map.shroudZones")
				] }),
				/* @__PURE__ */ E("label", { children: [
					/* @__PURE__ */ T("input", {
						type: "checkbox",
						checked: t.look.animate,
						onChange: (e) => d({ animate: e.target.checked })
					}),
					" ",
					o("admin.map.animateShroud")
				] }),
				/* @__PURE__ */ E("label", {
					title: o("admin.map.flameLevelTip"),
					children: [
						o("admin.map.flameLevel"),
						" ",
						/* @__PURE__ */ T("input", {
							type: "range",
							min: 1,
							max: 12,
							step: 1,
							value: t.look.flame,
							onChange: (e) => d({ flame: +e.target.value })
						}),
						" ",
						/* @__PURE__ */ T("b", { children: t.look.flame })
					]
				}),
				e.langs && e.langs.length > 1 ? /* @__PURE__ */ E("label", {
					title: o("admin.map.namesInTip"),
					children: [
						o("admin.map.namesIn"),
						" ",
						/* @__PURE__ */ T("select", {
							value: i ?? e.lang ?? "",
							onChange: (e) => a(e.target.value),
							children: e.langs.map((e) => /* @__PURE__ */ T("option", {
								value: e.code,
								children: e.name
							}, e.code))
						})
					]
				}) : null
			]
		}),
		/* @__PURE__ */ T("p", {
			className: "ka-map-muted",
			children: o("admin.map.markersOn", {
				shown: fa(u),
				total: fa(e.points.length),
				found: fa(t.found.length)
			})
		}),
		/* @__PURE__ */ T("p", {
			className: "ka-map-muted",
			"data-marker-source": e.source ?? "copy",
			children: e.source === "game" ? e.build ? o("admin.map.sourceGameBuild", { build: e.build }) : o("admin.map.sourceGame") : o("admin.map.sourceCopy")
		})
	] });
}
function Ea({ F: e, missing: t, ui: n, findQ: r, setFindQ: i, toggle: a, addAll: o, dropsBy: s, clear: c, setUi: l }) {
	let u = M();
	if (!e) return /* @__PURE__ */ T("p", {
		className: "ka-map-muted",
		children: u(t ? "admin.map.finderMissing" : "admin.map.loading")
	});
	let d = (e, t) => n.finders.some((n) => n.kind === e && n.slug === t), f = n.finders.map((t) => e[t.kind]?.[t.slug] ? {
		...e[t.kind][t.slug],
		kind: t.kind,
		slug: t.slug
	} : null).filter(Boolean), p = r.trim(), m = (t) => Object.entries(e[t]).map(([e, n]) => ({
		...n,
		kind: t,
		slug: e
	})).filter((e) => p.length < 2 || da(p, e.n)), h = (e, t) => e.n.localeCompare(t.n), g = [];
	if (p.length >= 3) for (let [t, n] of Object.entries(e.drops)) {
		if (!da(p, t)) continue;
		let r = n.by.map((t) => e.spawns[t] ? {
			...e.spawns[t],
			kind: "spawns",
			slug: t
		} : null).filter(Boolean);
		if (g.push({
			id: `drop:${t}`,
			label: u("admin.map.itemDroppedBy", { item: t }),
			img: n.img,
			list: r,
			all: !0
		}), g.length > 4) break;
	}
	let _ = m("spawns"), v = m("ore").sort((e, t) => Number(e.more) - Number(t.more) || (e.order ?? 0) - (t.order ?? 0));
	v.length && g.push({
		id: "ore",
		label: u("admin.map.oreGroup"),
		list: v,
		extra: /* @__PURE__ */ E("label", {
			className: "ka-map-inline",
			children: [
				/* @__PURE__ */ T("input", {
					type: "checkbox",
					checked: n.surfaceOnly,
					onChange: (e) => l({ surfaceOnly: e.target.checked })
				}),
				" ",
				u("admin.map.surfaceOnly")
			]
		})
	});
	let y = _.filter((e) => e.boss).sort(h), b = _.filter((e) => !e.boss).sort(h);
	y.length && g.push({
		id: "bosses",
		label: u("admin.map.bosses"),
		list: y
	}), b.length && g.push({
		id: "creatures",
		label: u("admin.map.creatures"),
		list: b
	});
	let x = {};
	for (let e of m("gather").sort(h)) (x[e.g ?? "Other"] ??= []).push(e);
	for (let [e, t] of Object.entries(x).sort()) g.push({
		id: `g:${e}`,
		label: e === "Other" ? u("admin.map.otherGroup") : e,
		list: t
	});
	let S = m("chests").sort((e, t) => t.c - e.c), C = p.length < 2 ? 150 : 80;
	S.length && g.push({
		id: "chests",
		label: u("admin.map.inChests"),
		list: S.slice(0, C),
		more: S.length > C ? S.length : 0
	});
	let ee = p.length >= 2;
	return /* @__PURE__ */ E(w, { children: [
		/* @__PURE__ */ E("section", {
			className: "ka-map-mine",
			children: [/* @__PURE__ */ E("h3", { children: [/* @__PURE__ */ T("span", { children: u("admin.map.myFilter") }), f.length ? /* @__PURE__ */ T(L, {
				size: "s",
				variant: "ghost",
				onClick: c,
				style: { marginLeft: "auto" },
				children: u("admin.map.clear")
			}) : null] }), f.length ? f.map((e) => /* @__PURE__ */ E("div", {
				className: "ka-map-mrow",
				children: [
					/* @__PURE__ */ E("span", { children: [e.img ? /* @__PURE__ */ T("img", {
						src: `${ra()}i/${e.img}`,
						alt: ""
					}) : /* @__PURE__ */ T("i", {
						className: "ka-dot",
						style: { background: e.color ?? Ji[e.kind] }
					}), e.n] }),
					/* @__PURE__ */ T("small", { children: ha(e.kind, e.c) }),
					/* @__PURE__ */ T("button", {
						className: "ka-map-x",
						title: u("common.remove"),
						onClick: () => a(e.kind, e.slug),
						children: "✕"
					})
				]
			}, `${e.kind}:${e.slug}`)) : /* @__PURE__ */ T("p", {
				className: "ka-map-muted",
				children: u("admin.map.filterEmpty")
			})]
		}),
		/* @__PURE__ */ T(R, {
			type: "search",
			"aria-label": u("admin.map.filterLabel"),
			placeholder: u("admin.map.filterPlaceholder"),
			value: r,
			onChange: (e) => i(e.target.value)
		}),
		g.map((e) => {
			let t = ee || e.id.startsWith("drop:") || n.open[`f:${e.id}`], r = e.list.filter((e) => d(e.kind, e.slug)).length;
			return /* @__PURE__ */ E("section", {
				className: "ka-map-group",
				children: [/* @__PURE__ */ E("h3", { children: [
					/* @__PURE__ */ E("span", {
						onClick: () => l({ open: {
							...n.open,
							[`f:${e.id}`]: !n.open[`f:${e.id}`]
						} }),
						children: [e.img ? /* @__PURE__ */ T("img", {
							src: `${ra()}i/${e.img}`,
							alt: "",
							style: {
								width: 20,
								height: 20
							}
						}) : null, e.label]
					}),
					e.all ? /* @__PURE__ */ T(L, {
						size: "s",
						variant: "ghost",
						onClick: () => o(e.list.map((e) => [e.kind, e.slug])),
						children: u("admin.map.showAll")
					}) : null,
					/* @__PURE__ */ E("small", {
						onClick: () => l({ open: {
							...n.open,
							[`f:${e.id}`]: !n.open[`f:${e.id}`]
						} }),
						children: [
							r ? u("admin.map.nOn", { n: r }) : "",
							fa(e.list.length),
							" ",
							t ? "▾" : "▸"
						]
					})
				] }), t ? /* @__PURE__ */ E(w, { children: [
					e.extra,
					/* @__PURE__ */ T("div", {
						className: "ka-map-cats",
						children: e.list.map((e) => /* @__PURE__ */ E("button", {
							type: "button",
							className: "ka-map-cat" + (d(e.kind, e.slug) ? "" : " off"),
							onClick: () => a(e.kind, e.slug),
							title: e.kind === "spawns" && s[e.slug] ? `${e.n}\n${u("admin.map.drops", { list: s[e.slug].join(", ") })}` : e.n,
							children: [
								e.img ? /* @__PURE__ */ T("img", {
									src: `${ra()}i/${e.img}`,
									alt: "",
									loading: "lazy"
								}) : /* @__PURE__ */ T("i", {
									className: "ka-dot",
									style: { background: e.color ?? Ji[e.kind] }
								}),
								/* @__PURE__ */ T("span", { children: e.n }),
								/* @__PURE__ */ T("small", { children: fa(e.c) })
							]
						}, `${e.kind}:${e.slug}`))
					}),
					e.more ? /* @__PURE__ */ T("p", {
						className: "ka-map-muted",
						children: u("admin.map.moreItems", { n: fa(e.more) })
					}) : null
				] }) : null]
			}, e.id);
		})
	] });
}
function Da({ players: e, connected: t, reason: n, waypoints: r, graves: i, zones: a, ui: o, setUi: s, enemies: c, enemyErr: l, hasEnemies: u, routeFrom: d, go: f, follow: p }) {
	let m = M();
	return /* @__PURE__ */ E(w, { children: [
		/* @__PURE__ */ T("p", {
			className: "ka-map-status" + (t ? " on" : ""),
			children: t ? m("admin.map.liveConnected", { n: e.length }) : n ?? m("admin.map.liveNotConnected")
		}),
		/* @__PURE__ */ E("section", {
			className: "ka-map-group",
			children: [/* @__PURE__ */ E("h3", { children: [/* @__PURE__ */ T("span", { children: m("admin.pages.players") }), /* @__PURE__ */ T("small", { children: e.length })] }), e.length ? e.map((e) => /* @__PURE__ */ E("div", {
				className: "ka-map-lrow",
				children: [
					/* @__PURE__ */ T("i", {
						className: "ka-dot",
						style: { background: "#7dff9b" }
					}),
					/* @__PURE__ */ T("b", { children: J(e) }),
					/* @__PURE__ */ T("small", {
						className: "k-mono",
						children: m("admin.map.playerRowPos", {
							x: Math.round(e.pos.x),
							z: Math.round(e.pos.z),
							h: Math.round(e.pos.y)
						})
					}),
					/* @__PURE__ */ E("span", {
						className: "ka-map-btns",
						children: [/* @__PURE__ */ T(L, {
							size: "s",
							variant: "ghost",
							onClick: () => f(e.pos.x, e.pos.z),
							children: m("common.show")
						}), /* @__PURE__ */ T(L, {
							size: "s",
							variant: o.follow === e.entity ? "secondary" : "ghost",
							onClick: () => p(e.entity),
							children: o.follow === e.entity ? m("admin.map.following") : m("admin.map.follow")
						})]
					})
				]
			}, e.entity)) : /* @__PURE__ */ T("p", {
				className: "ka-map-muted",
				children: m("admin.players.nobody")
			})]
		}),
		/* @__PURE__ */ E("section", {
			className: "ka-map-group",
			children: [/* @__PURE__ */ E("h3", { children: [/* @__PURE__ */ T("span", { children: m("admin.zones.title") }), /* @__PURE__ */ T("small", { children: a.length })] }), a.length ? a.map((e) => /* @__PURE__ */ E("div", {
				className: "ka-map-lrow",
				children: [
					/* @__PURE__ */ T("i", {
						className: "ka-dot",
						style: { background: "#ff8540" }
					}),
					/* @__PURE__ */ T("b", { children: e.name }),
					/* @__PURE__ */ T("small", { children: m("admin.map.metres", { n: Math.round(e.radius) }) }),
					/* @__PURE__ */ T("span", {
						className: "ka-map-btns",
						children: /* @__PURE__ */ T(L, {
							size: "s",
							variant: "ghost",
							onClick: () => f(e.pos.x, e.pos.z),
							children: m("common.show")
						})
					})
				]
			}, e.name)) : /* @__PURE__ */ T("p", {
				className: "ka-map-muted",
				children: m("admin.map.zonesEmpty")
			})]
		}),
		/* @__PURE__ */ E("section", {
			className: "ka-map-group",
			children: [/* @__PURE__ */ E("h3", { children: [/* @__PURE__ */ T("span", { children: m("admin.map.waypoints") }), /* @__PURE__ */ T("small", { children: r.length })] }), r.length ? r.map((e) => /* @__PURE__ */ E("div", {
				className: "ka-map-lrow",
				children: [
					/* @__PURE__ */ T("img", {
						src: `${ra()}i/g-mapmarker_waypoint.webp`,
						alt: ""
					}),
					/* @__PURE__ */ T("b", { children: e.name }),
					/* @__PURE__ */ E("small", {
						className: "k-mono",
						children: [
							Math.round(e.pos.x),
							", ",
							Math.round(e.pos.z)
						]
					}),
					/* @__PURE__ */ T("span", {
						className: "ka-map-btns",
						children: /* @__PURE__ */ T(L, {
							size: "s",
							variant: "ghost",
							onClick: () => f(e.pos.x, e.pos.z),
							children: m("common.show")
						})
					})
				]
			}, e.name)) : /* @__PURE__ */ T("p", {
				className: "ka-map-muted",
				children: m("admin.map.waypointsEmpty")
			})]
		}),
		/* @__PURE__ */ E("section", {
			className: "ka-map-group",
			children: [/* @__PURE__ */ E("h3", { children: [/* @__PURE__ */ T("span", { children: m("admin.map.graves") }), /* @__PURE__ */ T("small", { children: i.length })] }), i.length ? [...i].reverse().slice(0, 12).map((e) => /* @__PURE__ */ E("div", {
				className: "ka-map-lrow",
				children: [
					/* @__PURE__ */ T("i", {
						className: "ka-dot",
						style: { background: "#b9a7d6" }
					}),
					/* @__PURE__ */ T("b", { children: e.name ?? m("admin.map.aPlayer") }),
					/* @__PURE__ */ T("small", { children: Fr(e.at) }),
					/* @__PURE__ */ T("span", {
						className: "ka-map-btns",
						children: /* @__PURE__ */ T(L, {
							size: "s",
							variant: "ghost",
							onClick: () => f(e.pos.x, e.pos.z),
							children: m("common.show")
						})
					})
				]
			}, e.key)) : /* @__PURE__ */ T("p", {
				className: "ka-map-muted",
				children: m("admin.map.noDeaths")
			})]
		}),
		/* @__PURE__ */ E("section", {
			className: "ka-map-group",
			children: [
				/* @__PURE__ */ E("h3", { children: [/* @__PURE__ */ T("span", { children: m("admin.map.livingEnemies") }), /* @__PURE__ */ T("small", { children: o.enemies ? c : m("admin.map.off") })] }),
				/* @__PURE__ */ E("label", {
					className: "ka-map-inline",
					children: [
						/* @__PURE__ */ T("input", {
							type: "checkbox",
							disabled: !u,
							checked: o.enemies,
							onChange: (e) => s({ enemies: e.target.checked })
						}),
						" ",
						m("admin.map.showEnemies")
					]
				}),
				o.enemies && l ? /* @__PURE__ */ T("p", {
					className: "ka-map-muted",
					style: { color: "var(--k-warn)" },
					children: l
				}) : null
			]
		}),
		/* @__PURE__ */ E("section", {
			className: "ka-map-group",
			children: [/* @__PURE__ */ T("h3", { children: /* @__PURE__ */ T("span", { children: m("admin.map.route") }) }), o.route ? /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ E("p", { children: [
				d ?? m("admin.map.start"),
				" → ",
				o.route.to.name ?? m("admin.map.point")
			] }), /* @__PURE__ */ T(L, {
				size: "s",
				onClick: () => s({ route: null }),
				children: m("admin.map.clearRoute")
			})] }) : /* @__PURE__ */ T("p", {
				className: "ka-map-muted",
				children: m("admin.map.routeEmpty")
			})]
		})
	] });
}
// src/admin/pages/AdminMapPage.tsx
function Oa() {
	let e = M(), t = b(null), [n, r] = x(480);
	return v(() => {
		let e = () => {
			let e = t.current?.getBoundingClientRect().top ?? 0;
			r(Math.max(300, window.innerHeight - e - 18));
		};
		return e(), window.addEventListener("resize", e), () => window.removeEventListener("resize", e);
	}, []), /* @__PURE__ */ E("div", {
		ref: t,
		className: "ka-mapwrap-page",
		children: [/* @__PURE__ */ T("h1", {
			className: "k-sr-only",
			children: e("admin.pages.map")
		}), /* @__PURE__ */ T(ba, { height: n })]
	});
}
var ka = Br(Oa);
// src/admin/zoneHookState.ts
function Aa(e) {
	if (!e?.available) return [];
	let t = [];
	for (let [n, r] of [
		["build", "buildHooks"],
		["terraform", "terraformHook"],
		["spawn", "spawnHook"]
	]) {
		let i = e.hookGroups?.[n];
		if (i?.state === "installed" || (!i || i.state === "unknown") && e[r]) continue;
		let a = A(n === "build" ? "admin.zoneHooks.build" : n === "terraform" ? "admin.zoneHooks.terraform" : "admin.zoneHooks.spawn");
		t.push(i?.state === "idle" ? A("admin.zoneHooks.idle", { label: a }) : i?.state === "waiting" ? A("admin.zoneHooks.waiting", {
			label: a,
			reason: i.reason
		}) : i?.state === "refused" ? A("admin.zoneHooks.refused", {
			label: a,
			reason: i.reason.replace(/^refused:\s*/i, "")
		}) : A("admin.zoneHooks.unknown", { label: a }));
	}
	return e.intercept === !1 && t.push(A("admin.zoneHooks.noIntercept")), t;
}
// src/admin/pages/AdminZonesPage.tsx
function ja() {
	let e = M(), t = Y(), n = c(), r = i(), a = Ft().state?.players ?? [], o = I("zones_list", {}, { refetchInterval: 6e3 }), s = I("gameplay_status", {}, { refetchInterval: 1e4 }), l = I("permissions_get", {}, { staleTime: 3e4 }), u = Jt(t), [d, f] = x(Ei), p = Oi(), m = ki((e) => e.set);
	g(() => {
		p.draft && (f((e) => ({
			...e.editing ? Ei() : e,
			x: String(Zr(p.draft.x)),
			y: String(Zr(p.draft.y)),
			z: String(Zr(p.draft.z))
		})), p.set(null), G(e("admin.zones.centreFromMap"), e("admin.zones.centreFromMapDetail")));
	}, [p]);
	let h = (e) => n.setQueriesData({ queryKey: [
		"admin",
		t,
		"zones_list"
	] }, e), _ = F("zones_add", {
		onSuccess: (t) => {
			h(t), G(d.editing ? e("admin.zones.saved", { name: d.name.trim() }) : e("admin.zones.added", { name: d.name.trim() })), f(Ei());
		},
		onError: (e) => K(e),
		invalidates: ["gameplay_status"]
	}), v = F("zones_remove", {
		onSuccess: (e) => h(e),
		onError: (e) => K(e),
		invalidates: ["gameplay_status"]
	}), y = /* @__PURE__ */ new Map();
	for (let e of nn(u, l.data?.players ?? {})) e.name && y.set(e.steamId, e.name);
	for (let e of a) e.steamId && e.name && y.set(e.steamId, e.name);
	let b = async () => {
		let t = Di(d);
		if (t.error) return K({ message: t.error });
		(d.owners.length || await q({
			title: e("admin.zones.noOwnersTitle"),
			body: /* @__PURE__ */ T("p", { children: d.allowAdmins ? e("admin.zones.noOwnersAdmins") : e("admin.zones.noOwnersBody") }),
			ok: e("admin.zones.addAnyway")
		})) && _.mutate(t.args);
	}, S = s.data, C = (e) => f({
		name: e.name,
		x: String(e.pos.x),
		y: String(e.pos.y),
		z: String(e.pos.z),
		radius: String(e.radius),
		deny: [...e.deny],
		owners: [...e.owners],
		allowAdmins: e.allowAdmins,
		editing: !0
	}), w = Aa(S);
	return /* @__PURE__ */ E(Bn, {
		fill: !0,
		children: [
			/* @__PURE__ */ T(zn, {
				title: e("admin.zones.title"),
				sub: S?.available ? e("admin.zones.subDenied", { count: S.denied ?? 0 }) : e("admin.zones.sub")
			}),
			/* @__PURE__ */ T(li, {}),
			/* @__PURE__ */ E(Vr, {
				ops: [
					"zones_list",
					"zones_add",
					"zones_remove"
				],
				what: e("admin.zones.title"),
				children: [
					S && !S.available ? /* @__PURE__ */ T(H, {
						tone: "warn",
						title: e("admin.zones.notAvailable"),
						children: e("admin.zones.noGameplay")
					}) : null,
					w.length ? /* @__PURE__ */ T(H, {
						tone: "warn",
						title: e("admin.zones.status"),
						children: w.join(" ")
					}) : null,
					/* @__PURE__ */ T(Vn, {
						form: !0,
						aside: /* @__PURE__ */ T(Ma, {
							form: d,
							setForm: f,
							players: a,
							names: y,
							onSave: b,
							saving: _.isPending
						}),
						children: /* @__PURE__ */ T(Gn, {
							"aria-label": e("admin.zones.tableLabel"),
							fill: !0,
							rows: o.data ?? [],
							rowKey: (e) => e.name,
							selectedKey: d.editing ? d.name : null,
							onRowClick: C,
							columns: [
								{
									key: "n",
									header: e("admin.zones.colZone"),
									render: (t) => /* @__PURE__ */ T(Kn, {
										name: t.name,
										id: t.active === !1 ? e("admin.zones.inactive") : Rr(t.pos)
									}),
									sort: (e, t) => e.name.localeCompare(t.name)
								},
								{
									key: "d",
									header: e("admin.zones.colRefuses"),
									render: (e) => {
										let t = e.deny.map((e) => wi(e)).join(", ");
										return /* @__PURE__ */ T("span", {
											className: "k-muted",
											title: t,
											style: { fontSize: 15.5 },
											children: t
										});
									}
								},
								{
									key: "o",
									header: e("admin.zones.colOwners"),
									render: (t) => {
										let n = t.owners.map((e) => y.get(e) ?? e).join(", ");
										return /* @__PURE__ */ E("span", {
											title: t.owners.join(", "),
											style: { fontSize: 15.5 },
											children: [n || /* @__PURE__ */ T("span", {
												className: "k-muted",
												children: e("admin.zones.nobody")
											}), t.allowAdmins ? /* @__PURE__ */ T("span", {
												className: "k-muted",
												children: e("admin.zones.andAdmins")
											}) : null]
										});
									}
								},
								{
									key: "r",
									header: e("admin.zones.colRadius"),
									width: "70px",
									align: "right",
									render: (t) => /* @__PURE__ */ T("span", {
										className: "k-mono",
										children: e("admin.zones.metres", { n: Math.round(t.radius) })
									}),
									sort: (e, t) => e.radius - t.radius
								},
								{
									key: "a",
									header: /* @__PURE__ */ T("span", {
										className: "k-sr-only",
										children: e("admin.zones.colActions")
									}),
									width: "32px",
									align: "right",
									render: (t) => /* @__PURE__ */ T("span", {
										onClick: (e) => e.stopPropagation(),
										children: /* @__PURE__ */ T(br, {
											label: e("admin.zones.moreFor", { name: t.name }),
											icon: /* @__PURE__ */ T(ee, {}),
											size: "s",
											items: [
												{
													label: e("admin.zones.showOnMap"),
													onSelect: () => {
														m({
															x: t.pos.x,
															z: t.pos.z,
															zoom: 6
														}), r("../map");
													}
												},
												{
													label: e("admin.zones.change"),
													onSelect: () => C(t)
												},
												{
													label: e("common.remove"),
													danger: !0,
													onSelect: async () => {
														await q({
															title: e("admin.zones.removeTitle", { name: t.name }),
															body: /* @__PURE__ */ T("p", { children: e("admin.zones.removeBody") }),
															ok: e("common.remove"),
															danger: !0
														}) && v.mutate({ name: t.name }, { onSuccess: () => G(e("admin.zones.removed", { name: t.name })) });
													}
												}
											]
										})
									})
								}
							],
							empty: e("admin.zones.empty")
						})
					})
				]
			})
		]
	});
}
function Ma({ form: e, setForm: t, players: n, names: r, onSave: i, saving: a }) {
	let o = M(), [s, c] = x(""), [l, u] = x(""), d = (n) => t({
		...e,
		...n
	}), f = [.../* @__PURE__ */ new Set([
		...n.map((e) => e.steamId).filter(Boolean),
		...e.owners,
		...r.keys()
	])];
	return /* @__PURE__ */ E("form", {
		className: "k-stack",
		style: { gap: 12 },
		onSubmit: (e) => {
			e.preventDefault(), i();
		},
		children: [
			/* @__PURE__ */ E("div", { children: [/* @__PURE__ */ T("span", {
				className: "k-card-title",
				children: e.editing ? o("admin.zones.changeName", { name: e.name }) : o("admin.zones.addZoneTitle")
			}), /* @__PURE__ */ T("div", {
				className: "k-hint",
				children: o("admin.zones.formHint")
			})] }),
			/* @__PURE__ */ T(sn, {
				label: o("admin.zones.name"),
				hint: e.editing ? o("admin.zones.nameHintEdit") : o("admin.zones.nameHintAdd"),
				htmlFor: "zone-name",
				children: /* @__PURE__ */ T(R, {
					id: "zone-name",
					value: e.name,
					maxLength: 64,
					readOnly: e.editing,
					placeholder: o("admin.zones.namePlaceholder"),
					onChange: (e) => d({ name: e.target.value })
				})
			}),
			/* @__PURE__ */ T(sn, {
				label: o("admin.zones.radiusLabel"),
				htmlFor: "zone-r",
				children: /* @__PURE__ */ T(R, {
					id: "zone-r",
					inputMode: "numeric",
					value: e.radius,
					onChange: (e) => d({ radius: e.target.value })
				})
			}),
			/* @__PURE__ */ E(sn, {
				label: o("admin.zones.centre"),
				children: [/* @__PURE__ */ T("div", {
					className: "ka-coords",
					children: [
						"x",
						"y",
						"z"
					].map((t) => /* @__PURE__ */ T(R, {
						className: "ka-coord",
						"aria-label": t === "y" ? o("admin.zones.yHeight") : t,
						placeholder: t,
						value: e[t],
						onChange: (e) => d({ [t]: e.target.value })
					}, t))
				}), n.length ? /* @__PURE__ */ E("div", {
					className: "ka-inline",
					style: { flexWrap: "nowrap" },
					children: [/* @__PURE__ */ T(z, {
						"aria-label": o("admin.zones.player"),
						value: s || String(n[0].entity),
						onChange: c,
						options: n.map((e) => ({
							value: String(e.entity),
							label: o("admin.zones.whereStands", { name: J(e) })
						}))
					}), /* @__PURE__ */ T(L, {
						size: "s",
						onClick: () => {
							let t = n.find((e) => e.entity === Number(s || n[0].entity));
							if (!t?.pos) return K({ message: o("admin.zones.noPlayerPos") });
							d({
								x: String(Zr(t.pos.x)),
								y: String(Zr(t.pos.y)),
								z: String(Zr(t.pos.z)),
								owners: !e.editing && t.steamId && !e.owners.includes(t.steamId) ? [...e.owners, t.steamId] : e.owners
							});
						},
						children: o("admin.zones.use")
					})]
				}) : null]
			}),
			/* @__PURE__ */ T(sn, {
				label: o("admin.zones.refuseInside"),
				children: /* @__PURE__ */ T("div", {
					className: "ka-flags",
					children: Si.map((t) => /* @__PURE__ */ E("label", {
						title: Ti(t),
						children: [
							/* @__PURE__ */ T("input", {
								type: "checkbox",
								checked: e.deny.includes(t),
								onChange: (n) => d({ deny: n.target.checked ? [...e.deny, t] : e.deny.filter((e) => e !== t) })
							}),
							" ",
							wi(t)
						]
					}, t))
				})
			}),
			/* @__PURE__ */ E(sn, {
				label: o("admin.zones.ownersLabel"),
				children: [/* @__PURE__ */ T("div", {
					className: "ka-flags",
					children: f.length ? f.map((t) => /* @__PURE__ */ E("label", {
						title: t,
						children: [/* @__PURE__ */ T("input", {
							type: "checkbox",
							checked: e.owners.includes(t),
							onChange: (n) => d({ owners: n.target.checked ? [...e.owners, t] : e.owners.filter((e) => e !== t) })
						}), r.get(t) ?? t]
					}, t)) : /* @__PURE__ */ T("span", {
						className: "k-hint",
						children: o("admin.zones.noKnownPlayers")
					})
				}), /* @__PURE__ */ E("div", {
					className: "ka-inline",
					style: { flexWrap: "nowrap" },
					children: [/* @__PURE__ */ T(R, {
						"aria-label": o("admin.zones.addOwner"),
						placeholder: "SteamID64",
						maxLength: 17,
						value: l,
						onChange: (e) => u(e.target.value),
						mono: !0
					}), /* @__PURE__ */ T(L, {
						size: "s",
						onClick: () => {
							let t = ai(l, !0);
							if (t) return K({ message: t });
							e.owners.includes(l.trim()) || d({ owners: [...e.owners, l.trim()] }), u("");
						},
						children: o("common.add")
					})]
				})]
			}),
			/* @__PURE__ */ T(ln, {
				checked: e.allowAdmins,
				onChange: (e) => d({ allowAdmins: e }),
				label: o("admin.zones.adminsToo")
			}),
			/* @__PURE__ */ E("div", {
				className: "k-row",
				style: { justifyContent: "flex-end" },
				children: [e.editing ? /* @__PURE__ */ T(L, {
					variant: "ghost",
					onClick: () => t(Ei()),
					children: o("common.cancel")
				}) : null, /* @__PURE__ */ T(L, {
					type: "submit",
					variant: "primary",
					loading: a,
					children: e.editing ? o("admin.zones.saveZone") : o("admin.zones.addZone")
				})]
			})
		]
	});
}
var Na = Br(ja), Pa = [
	"Common",
	"Uncommon",
	"Rare",
	"Epic",
	"Legendary"
];
function Fa() {
	let e = M(), t = Ft().state?.players ?? [], [n, r] = x(""), i = zr(n.trim(), 200), a = I("item_search", { q: i }, {
		enabled: i.length >= 2,
		staleTime: 6e4
	}), [o, s] = x(null), c = qr(), l = y(() => (a.data ?? []).map((e) => ({
		...e,
		...Jr(e, c)
	})), [a.data, c]), u = l.find((e) => e.id === o) ?? l[0] ?? null, d = i.length < 2 ? "" : a.isFetching ? e("admin.items.searching") : l.length === 50 ? e("admin.items.foundMore", { n: l.length }) : e("admin.items.found", { n: l.length });
	return /* @__PURE__ */ E(Bn, {
		fill: !0,
		children: [
			/* @__PURE__ */ T("h1", {
				className: "k-sr-only",
				children: e("admin.pages.items")
			}),
			/* @__PURE__ */ T(li, {}),
			/* @__PURE__ */ T(Vr, {
				ops: "item_search",
				what: e("admin.items.what"),
				children: /* @__PURE__ */ E(Vn, {
					form: !0,
					aside: u ? /* @__PURE__ */ T(Ia, {
						item: u,
						players: t
					}, u.id) : /* @__PURE__ */ T("span", {
						className: "k-empty-title",
						children: e("admin.items.pickAside")
					}),
					children: [/* @__PURE__ */ T(fn, {
						label: e("admin.items.searchLabel"),
						autoFocus: !0,
						value: n,
						onChange: (e) => r(e.target.value),
						onClear: () => r(""),
						note: d,
						style: { fontSize: 16 }
					}), /* @__PURE__ */ T(Gn, {
						"aria-label": e("admin.pages.items"),
						fill: !0,
						rows: l,
						rowKey: (e) => String(e.id),
						rowHeight: 44,
						selectedKey: u ? String(u.id) : null,
						onRowClick: (e) => s(e.id),
						columns: [
							{
								key: "n",
								header: e("admin.items.colItem"),
								sort: (e, t) => e.name.localeCompare(t.name),
								render: (e) => /* @__PURE__ */ T(Kn, {
									name: e.name,
									id: e.debug ?? void 0
								})
							},
							{
								key: "g",
								header: e("admin.items.colKind"),
								width: "64px",
								render: (t) => /* @__PURE__ */ T("span", {
									className: `k-badge k-tone-${t.gear ? "ember" : "idle"}`,
									children: t.gear ? e("admin.items.gear") : e("admin.items.stacks")
								})
							},
							{
								key: "id",
								header: e("admin.items.colId"),
								width: "110px",
								align: "right",
								render: (e) => /* @__PURE__ */ T("span", {
									className: "k-mono",
									style: { color: "#d0beac" },
									children: e.id
								})
							}
						],
						empty: i.length >= 2 ? a.isFetching ? e("admin.items.searchingDot") : a.error?.message ?? e("admin.items.noMatch") : e("admin.items.typeTwo")
					})]
				})
			})
		]
	});
}
function Ia({ item: e, players: t }) {
	let n = M(), [r, i] = x(null), a = r ?? t[0]?.entity ?? null, [o, s] = x(1), [c, l] = x(null), [u, d] = x(""), [f, p] = x(""), m = f || (t[0] ? `p:${t[0].entity}` : "coords"), [h, g] = x(Xr(null)), [_, v] = x(1), y = F("give_item", {
		onSuccess: (t) => G(n("admin.players.gave", {
			given: t.given,
			item: e.name
		}), t.given < t.requested ? n("admin.items.onlyRoom", { given: t.given }) : void 0),
		onError: (e) => K(e)
	}), b = F("spawn_item", {
		onSuccess: (t) => G(n("admin.items.dropped", { item: e.name }), n("admin.items.entityDetail", { entity: t.entity })),
		onError: (e) => K(e)
	}), S = P("give_item"), C = P("spawn_item"), ee = () => {
		if (a == null) return K({ message: n("admin.items.pickPlayer") });
		if (!Number.isInteger(o) || o < 1 || o > 1e4) return K({ message: n("admin.players.amountRange") });
		let t = {
			entity: a,
			item: e.id,
			count: o
		};
		if (e.gear && c != null) {
			if (c < 1 || c > 100) return K({ message: n("admin.players.levelRange") });
			t.level = c;
		}
		e.gear && u && (t.rarity = u), y.mutate(t);
	}, D = () => {
		let r;
		if (m.startsWith("p:")) r = t.find((e) => e.entity === Number(m.slice(2)))?.pos;
		else {
			let e = Qr(h);
			if (e.error) return K({ message: e.error });
			r = e.pos;
		}
		if (!r) return K({ message: n("admin.zones.noPlayerPos") });
		if (!Number.isInteger(_) || _ < 1 || _ > 1e4) return K({ message: n("admin.players.amountRange") });
		b.mutate({
			item: e.id,
			amount: _,
			x: r.x + (m.startsWith("p:") ? 1.5 : 0),
			y: r.y + .5,
			z: r.z
		});
	};
	return /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ E("form", {
		className: "k-stack",
		style: { gap: 10 },
		onSubmit: (e) => {
			e.preventDefault(), ee();
		},
		children: [/* @__PURE__ */ E("div", { children: [/* @__PURE__ */ T("span", {
			style: {
				fontSize: 20,
				fontWeight: 500,
				color: "var(--k-text-0)",
				overflowWrap: "anywhere"
			},
			children: e.name
		}), /* @__PURE__ */ E("div", {
			className: "k-mono k-muted",
			style: {
				fontSize: 14.5,
				overflowWrap: "anywhere"
			},
			children: [
				e.debug ? `${e.debug}, ` : "",
				"#",
				e.id,
				", ",
				e.gear ? n("admin.items.gear") : n("admin.items.stacks")
			]
		})] }), S ? /* @__PURE__ */ E(w, { children: [
			/* @__PURE__ */ T(sn, {
				label: n("admin.items.giveTo"),
				htmlFor: "give-to",
				children: /* @__PURE__ */ T(Ur, {
					id: "give-to",
					value: a,
					onChange: i,
					players: t,
					placeholder: n("admin.common.pickPlayer")
				})
			}),
			/* @__PURE__ */ E("div", {
				style: {
					display: "grid",
					gridTemplateColumns: e.gear ? "1fr 1fr 1.2fr" : "1fr 2fr",
					gap: 8
				},
				children: [/* @__PURE__ */ T(sn, {
					label: n("admin.players.amount"),
					htmlFor: "give-n",
					children: /* @__PURE__ */ T(B, {
						id: "give-n",
						value: o,
						integer: !0,
						min: 1,
						max: 1e4,
						onCommit: s
					})
				}), e.gear ? /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(sn, {
					label: n("admin.players.level"),
					htmlFor: "give-l",
					children: /* @__PURE__ */ T(B, {
						id: "give-l",
						value: c,
						integer: !0,
						min: 1,
						max: 100,
						onCommit: l,
						placeholder: n("admin.items.levelPlaceholder")
					})
				}), /* @__PURE__ */ T(sn, {
					label: n("admin.players.rarity"),
					htmlFor: "give-r",
					children: /* @__PURE__ */ T(z, {
						id: "give-r",
						value: u,
						onChange: d,
						options: [{
							value: "",
							label: n("admin.items.rarityOwn")
						}, ...Pa.map((e) => ({
							value: e,
							label: Wr(e)
						}))]
					})
				})] }) : /* @__PURE__ */ T("span", {})]
			}),
			/* @__PURE__ */ E("div", {
				className: "k-row-between",
				children: [/* @__PURE__ */ T("span", {
					className: "k-hint",
					children: a == null ? n("admin.items.needsOnline") : n("admin.items.toPlayer", { name: J(t.find((e) => e.entity === a)) })
				}), /* @__PURE__ */ T(L, {
					type: "submit",
					variant: "primary",
					disabled: a == null,
					loading: y.isPending,
					children: n("admin.players.give")
				})]
			})
		] }) : /* @__PURE__ */ T("span", {
			className: "k-hint",
			children: n("admin.items.noGiveOp")
		})]
	}), C && /* @__PURE__ */ E("form", {
		className: "k-stack",
		style: {
			gap: 10,
			paddingTop: 14,
			boxShadow: "inset 0 1px 0 var(--k-line)"
		},
		onSubmit: (e) => {
			e.preventDefault(), D();
		},
		children: [
			/* @__PURE__ */ E("div", { children: [/* @__PURE__ */ T("span", {
				className: "k-card-title",
				children: n("admin.items.dropTitle")
			}), /* @__PURE__ */ T("div", {
				className: "k-hint",
				children: n("admin.items.dropHint")
			})] }),
			t.length > 0 && /* @__PURE__ */ T(z, {
				"aria-label": n("admin.items.where"),
				value: m,
				onChange: p,
				options: [...t.map((e) => ({
					value: `p:${e.entity}`,
					label: n("admin.items.atPlayer", { name: J(e) })
				})), {
					value: "coords",
					label: n("admin.items.atCoords")
				}]
			}),
			m === "coords" && /* @__PURE__ */ T($r, {
				value: h,
				onChange: g
			}),
			/* @__PURE__ */ E("div", {
				className: "k-row-between",
				children: [/* @__PURE__ */ T("div", {
					style: { width: 110 },
					children: /* @__PURE__ */ T(B, {
						"aria-label": n("admin.items.amountDrop"),
						value: _,
						integer: !0,
						min: 1,
						max: 1e4,
						onCommit: v
					})
				}), /* @__PURE__ */ T(L, {
					type: "submit",
					loading: b.isPending,
					children: n("admin.items.drop")
				})]
			})
		]
	})] });
}
var La = Br(Fa), Ra = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
function za() {
	let e = M(), t = Ft(), n = Y(), r = Jt(n), i = t.state?.players ?? [], a = bi(), [o, s] = x(""), c = o || (i[0] ? `p:${i[0].entity}` : "coords"), [l, u] = x(Xr(null)), [d, f] = x(1), [p, m] = x(3), [h, g] = x(""), [_, v] = x(null), b = c.startsWith("p:") ? i.find((e) => e.entity === Number(c.slice(2)))?.pos : Qr(l).pos, S = I("enemies", b ? {
		near: {
			x: b.x,
			y: b.y,
			z: b.z
		},
		radius: 80
	} : {}, { refetchInterval: 15e3 }), C = F("spawn"), w = F("despawn_spawned", {
		onSuccess: (t) => G(e("admin.spawn.removed", { n: t.removed })),
		onError: (e) => K(e)
	}), ee = P("despawn_spawned"), D = y(() => {
		let e = /* @__PURE__ */ new Map();
		for (let t of a.data ?? []) e.set(t.guid, {
			guid: t.guid,
			label: t.name,
			template: t.template
		});
		for (let t of S.data?.catalog ?? []) e.has(t.guid) || e.set(t.guid, {
			guid: t.guid,
			label: r.enemyLabels[t.guid] || xi(t.name),
			template: t.name
		});
		return [...e.values()];
	}, [
		a.data,
		S.data,
		r.enemyLabels
	]), te = y(() => {
		let e = h.trim().toLowerCase();
		return e.length < 2 || _?.label === h ? [] : D.filter((t) => t.label.toLowerCase().includes(e) || (t.template ?? "").toLowerCase().includes(e) || t.guid.startsWith(e)).slice(0, 30).map((e) => ({
			key: e.guid,
			label: e.label,
			sub: /* @__PURE__ */ T("span", {
				className: "k-mono",
				children: e.template ?? e.guid.slice(0, 13)
			}),
			value: e
		}));
	}, [
		D,
		h,
		_
	]), O = (t, n = d) => {
		let r;
		if (c.startsWith("p:")) {
			let t = i.find((e) => e.entity === Number(c.slice(2)));
			if (!t?.pos) return K({ message: e("admin.zones.noPlayerPos") });
			r = {
				x: t.pos.x + 6,
				y: t.pos.y,
				z: t.pos.z
			};
		} else {
			let e = Qr(l);
			if (e.error) return K({ message: e.error });
			r = e.pos;
		}
		if (!Number.isInteger(n) || n < 1 || n > 20) return K({ message: e("admin.spawn.countRange") });
		if (!Ra.test(t.guid)) return K({ message: e("admin.spawn.notGuid") });
		C.mutate({
			guid: t.guid,
			count: n,
			x: r.x,
			y: r.y,
			z: r.z,
			spread: p
		}, {
			onSuccess: (n) => G(n.failed ? e("admin.spawn.spawnedFailed", {
				spawned: n.spawned,
				label: t.label,
				failed: n.failed
			}) : e("admin.spawn.spawned", {
				spawned: n.spawned,
				label: t.label
			})),
			onError: (e) => K(e)
		});
	}, ne = () => _ ?? (Ra.test(h.trim()) ? {
		guid: h.trim(),
		label: h.trim()
	} : null), k = t.state?.spawned ?? 0;
	return /* @__PURE__ */ E(Bn, {
		fill: !0,
		children: [
			/* @__PURE__ */ T(zn, {
				title: e("admin.spawn.title"),
				sub: S.data ? e("admin.spawn.subLoaded", {
					alive: k,
					known: D.length,
					loaded: S.data.total
				}) : e("admin.spawn.sub", {
					alive: k,
					known: D.length
				})
			}),
			/* @__PURE__ */ T(li, {}),
			/* @__PURE__ */ T(Vr, {
				ops: "spawn",
				what: e("admin.spawn.what"),
				children: /* @__PURE__ */ E(Vn, {
					form: !0,
					aside: /* @__PURE__ */ E("form", {
						className: "k-stack",
						style: { gap: 12 },
						onSubmit: (t) => {
							t.preventDefault();
							let n = ne();
							n ? O(n) : K({ message: e("admin.spawn.pickCreature") });
						},
						children: [
							/* @__PURE__ */ T("span", {
								className: "k-card-title",
								children: e("admin.spawn.formTitle")
							}),
							/* @__PURE__ */ T(sn, {
								label: e("admin.spawn.creature"),
								htmlFor: "spawn-what",
								hint: _ ? /* @__PURE__ */ T("span", {
									className: "k-mono",
									children: _.guid
								}) : void 0,
								children: /* @__PURE__ */ T(Nr, {
									id: "spawn-what",
									value: h,
									placeholder: D.length ? e("admin.spawn.searchCreatures", { n: D.length }) : e("admin.spawn.pasteGuid"),
									suggestions: te,
									onChange: (e) => {
										g(e), v(null);
									},
									onPick: (e) => {
										v(e.value), g(e.value.label);
									},
									onEnter: () => {
										let e = ne();
										e && O(e);
									}
								})
							}),
							/* @__PURE__ */ E(sn, {
								label: e("admin.items.where"),
								htmlFor: "spawn-where",
								children: [/* @__PURE__ */ T(z, {
									id: "spawn-where",
									value: c,
									onChange: s,
									options: [...i.map((t) => ({
										value: `p:${t.entity}`,
										label: e("admin.spawn.nextTo", { name: J(t) })
									})), {
										value: "coords",
										label: e("admin.items.atCoords")
									}]
								}), c === "coords" ? /* @__PURE__ */ T($r, {
									value: l,
									onChange: u
								}) : null]
							}),
							/* @__PURE__ */ E("div", {
								style: {
									display: "grid",
									gridTemplateColumns: "1fr 1fr",
									gap: 8
								},
								children: [/* @__PURE__ */ T(sn, {
									label: e("admin.spawn.howMany"),
									htmlFor: "spawn-n",
									children: /* @__PURE__ */ T(B, {
										id: "spawn-n",
										value: d,
										integer: !0,
										min: 1,
										max: 20,
										onCommit: f
									})
								}), /* @__PURE__ */ T(sn, {
									label: e("admin.spawn.spread"),
									htmlFor: "spawn-s",
									children: /* @__PURE__ */ T(B, {
										id: "spawn-s",
										value: p,
										min: 0,
										max: 50,
										onCommit: m
									})
								})]
							}),
							/* @__PURE__ */ T("div", {
								className: "k-row",
								style: { justifyContent: "flex-end" },
								children: /* @__PURE__ */ T(L, {
									type: "submit",
									variant: "primary",
									loading: C.isPending,
									children: e("admin.spawn.title")
								})
							}),
							a.data?.length ? null : /* @__PURE__ */ T("p", {
								className: "k-hint",
								children: e("admin.spawn.noCatalog")
							}),
							ee ? /* @__PURE__ */ E("div", {
								className: "ka-tool",
								children: [/* @__PURE__ */ T("span", {
									className: "k-hint",
									children: e("admin.spawn.alive", { count: k })
								}), /* @__PURE__ */ T("div", { children: /* @__PURE__ */ T(L, {
									variant: "danger",
									size: "s",
									disabled: !k,
									loading: w.isPending,
									onClick: async () => {
										await q({
											title: e("admin.spawn.removeTitle"),
											body: /* @__PURE__ */ T("p", { children: e("admin.spawn.removeBody", { n: k }) }),
											ok: e("common.remove"),
											danger: !0
										}) && w.mutate({});
									},
									children: k ? e("admin.spawn.removeN", { n: k }) : e("admin.spawn.removeNone")
								}) })]
							}) : null
						]
					}),
					children: [/* @__PURE__ */ T("div", {
						className: "k-card-head",
						children: /* @__PURE__ */ E("div", {
							className: "k-card-titles",
							children: [/* @__PURE__ */ T("h2", {
								className: "k-card-title",
								children: e("admin.spawn.seenTitle")
							}), /* @__PURE__ */ T("span", {
								className: "k-card-sub",
								children: e("admin.spawn.seenSub")
							})]
						})
					}), /* @__PURE__ */ T(Gn, {
						"aria-label": e("admin.spawn.seenTable"),
						fill: !0,
						rows: S.data?.catalog ?? [],
						rowKey: (e) => e.guid,
						columns: [
							{
								key: "label",
								header: e("admin.spawn.colName"),
								render: (t) => /* @__PURE__ */ T(R, {
									"aria-label": e("admin.spawn.nameFor", { name: t.name }),
									defaultValue: r.enemyLabels[t.guid] ?? a.data?.find((e) => e.guid === t.guid)?.name ?? xi(t.name),
									onBlur: (e) => tn(n, t.guid, e.target.value),
									style: { height: 32 }
								})
							},
							{
								key: "t",
								header: e("admin.spawn.colTemplate"),
								render: (e) => /* @__PURE__ */ T("span", {
									className: "k-mono k-muted",
									children: e.name
								})
							},
							{
								key: "seen",
								header: e("admin.spawn.colSeen"),
								width: "60px",
								align: "right",
								render: (e) => /* @__PURE__ */ T("span", {
									className: "k-mono",
									children: e.seen
								})
							},
							{
								key: "go",
								header: /* @__PURE__ */ T("span", {
									className: "k-sr-only",
									children: e("admin.spawn.title")
								}),
								width: "84px",
								align: "right",
								render: (t) => /* @__PURE__ */ T(L, {
									size: "s",
									disabled: C.isPending,
									onClick: () => O({
										guid: t.guid,
										label: r.enemyLabels[t.guid] || xi(t.name)
									}, 1),
									children: e("admin.spawn.spawnOne")
								})
							}
						],
						empty: S.data ? e("admin.spawn.seenEmpty") : e("admin.spawn.notScanned")
					})]
				})
			})
		]
	});
}
var Ba = Br(za), Q = (e, t, n, r, i, a) => ({
	key: e,
	group: t,
	label: n,
	kind: "factor",
	min: r,
	max: i,
	hint: a
}), Va = (e, t, n, r) => ({
	key: e,
	group: t,
	label: n,
	kind: "bool",
	hint: r
}), Ha = (e, t, n, r, i) => ({
	key: e,
	group: t,
	label: n,
	kind: "enum",
	options: r,
	hint: i
}), Ua = (e, t, n, r, i, a) => ({
	key: e,
	group: t,
	label: n,
	kind: "minutes",
	min: r,
	max: i,
	hint: a
}), Wa = () => A("admin.gameSettings.minutes"), Ga = [
	Q("playerHealthFactor", "Player", () => A("admin.gameSettings.playerHealthFactor"), .25, 4),
	Q("playerManaFactor", "Player", () => A("admin.gameSettings.playerManaFactor"), .25, 4),
	Q("playerStaminaFactor", "Player", () => A("admin.gameSettings.playerStaminaFactor"), .25, 4),
	Q("playerBodyHeatFactor", "Player", () => A("admin.gameSettings.playerBodyHeatFactor"), .5, 2, () => A("admin.gameSettings.playerBodyHeatFactorHint")),
	Q("playerDivingTimeFactor", "Player", () => A("admin.gameSettings.playerDivingTimeFactor"), .5, 2),
	Q("shroudTimeFactor", "Player", () => A("admin.gameSettings.shroudTimeFactor"), .5, 2),
	Va("enableDurability", "Survival", () => A("admin.gameSettings.enableDurability")),
	Va("enableStarvingDebuff", "Survival", () => A("admin.gameSettings.enableStarvingDebuff")),
	Q("foodBuffDurationFactor", "Survival", () => A("admin.gameSettings.foodBuffDurationFactor"), .5, 2),
	Ua("fromHungerToStarving", "Survival", () => A("admin.gameSettings.fromHungerToStarving"), 5, 20, Wa),
	Ha("tombstoneMode", "Survival", () => A("admin.gameSettings.tombstoneMode"), [
		"AddBackpackMaterials",
		"Everything",
		"NoTombstone"
	], () => A("admin.gameSettings.tombstoneModeHint")),
	Va("enableGliderTurbulences", "Survival", () => A("admin.gameSettings.enableGliderTurbulences")),
	Ha("weatherFrequency", "World", () => A("admin.gameSettings.weatherFrequency"), [
		"Disabled",
		"Rare",
		"Normal",
		"Often"
	]),
	Ha("randomSpawnerAmount", "World", () => A("admin.gameSettings.randomSpawnerAmount"), [
		"Few",
		"Normal",
		"Many",
		"Extreme",
		"Automatic"
	]),
	{
		key: "flameLevelFogRemoval",
		group: "World",
		label: () => A("admin.gameSettings.flameLevelFogRemoval"),
		kind: "int",
		min: 0,
		max: 20,
		hint: () => A("admin.gameSettings.flameLevelFogRemovalHint")
	},
	Ua("dayTimeDuration", "Time", () => A("admin.gameSettings.dayTimeDuration"), 2, 60, Wa),
	Ua("nightTimeDuration", "Time", () => A("admin.gameSettings.nightTimeDuration"), 2, 60, Wa),
	Q("miningDamageFactor", "Rates", () => A("admin.gameSettings.miningDamageFactor"), .5, 2),
	Q("plantGrowthSpeedFactor", "Rates", () => A("admin.gameSettings.plantGrowthSpeedFactor"), .25, 2),
	Q("resourceDropStackAmountFactor", "Rates", () => A("admin.gameSettings.resourceDropStackAmountFactor"), .25, 2),
	Q("factoryProductionSpeedFactor", "Rates", () => A("admin.gameSettings.factoryProductionSpeedFactor"), .25, 2),
	Q("perkUpgradeRecyclingFactor", "Rates", () => A("admin.gameSettings.perkUpgradeRecyclingFactor"), 0, 1),
	Q("perkCostFactor", "Rates", () => A("admin.gameSettings.perkCostFactor"), .25, 2),
	Q("experienceCombatFactor", "Rates", () => A("admin.gameSettings.experienceCombatFactor"), .25, 2),
	Q("experienceMiningFactor", "Rates", () => A("admin.gameSettings.experienceMiningFactor"), 0, 2),
	Q("experienceExplorationQuestsFactor", "Rates", () => A("admin.gameSettings.experienceExplorationQuestsFactor"), .25, 2),
	Ha("aggroPoolAmount", "Enemies", () => A("admin.gameSettings.aggroPoolAmount"), [
		"Few",
		"Normal",
		"Many",
		"Extreme"
	]),
	Q("enemyDamageFactor", "Enemies", () => A("admin.gameSettings.enemyDamageFactor"), .25, 5),
	Q("enemyHealthFactor", "Enemies", () => A("admin.gameSettings.enemyHealthFactor"), .25, 4),
	Q("enemyStaminaFactor", "Enemies", () => A("admin.gameSettings.enemyStaminaFactor"), .5, 2),
	Q("enemyPerceptionRangeFactor", "Enemies", () => A("admin.gameSettings.enemyPerceptionRangeFactor"), .5, 2),
	Q("bossDamageFactor", "Enemies", () => A("admin.gameSettings.bossDamageFactor"), .2, 5),
	Q("bossHealthFactor", "Enemies", () => A("admin.gameSettings.bossHealthFactor"), .2, 5),
	Q("threatBonus", "Enemies", () => A("admin.gameSettings.threatBonus"), .25, 4),
	Va("pacifyAllEnemies", "Enemies", () => A("admin.gameSettings.pacifyAllEnemies"), () => A("admin.gameSettings.pacifyAllEnemiesHint")),
	Ha("curseModifier", "Enemies", () => A("admin.gameSettings.curseModifier"), [
		"Easy",
		"Normal",
		"Hard"
	]),
	Ha("tamingStartleRepercussion", "Other", () => A("admin.gameSettings.tamingStartleRepercussion"), [
		"KeepProgress",
		"LoseSomeProgress",
		"LoseAllProgress"
	]),
	Ha("fishingDifficulty", "Other", () => A("admin.gameSettings.fishingDifficulty"), [
		"VeryEasy",
		"Easy",
		"Normal",
		"Hard",
		"VeryHard"
	])
];
function Ka(e) {
	switch (e) {
		case "Player": return A("admin.gameSettings.group.player");
		case "Survival": return A("admin.gameSettings.group.survival");
		case "World": return A("admin.gameSettings.group.world");
		case "Time": return A("admin.gameSettings.group.time");
		case "Rates": return A("admin.gameSettings.group.rates");
		case "Enemies": return A("admin.gameSettings.group.enemies");
		case "Other": return A("admin.gameSettings.group.others");
		default: return e;
	}
}
function qa(e) {
	switch (e) {
		case "AddBackpackMaterials": return A("admin.gameSettings.option.addBackpackMaterials");
		case "Everything": return A("admin.gameSettings.option.everything");
		case "NoTombstone": return A("admin.gameSettings.option.noTombstone");
		case "Disabled": return A("admin.gameSettings.option.disabled");
		case "Rare": return A("admin.gameSettings.option.rare");
		case "Normal": return A("admin.gameSettings.option.normal");
		case "Often": return A("admin.gameSettings.option.often");
		case "Few": return A("admin.gameSettings.option.fewAmount");
		case "Many": return A("admin.gameSettings.option.manyAmount");
		case "Extreme": return A("admin.gameSettings.option.extreme");
		case "Automatic": return A("admin.gameSettings.option.automatic");
		case "Easy": return A("admin.gameSettings.option.easy");
		case "Hard": return A("admin.gameSettings.option.hard");
		case "KeepProgress": return A("admin.gameSettings.option.keepProgress");
		case "LoseSomeProgress": return A("admin.gameSettings.option.loseSomeProgress");
		case "LoseAllProgress": return A("admin.gameSettings.option.loseAllProgress");
		case "VeryEasy": return A("admin.gameSettings.option.veryEasy");
		case "VeryHard": return A("admin.gameSettings.option.veryHard");
		default: return String(e).replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, (e) => e.toUpperCase());
	}
}
var Ja = 6e10;
function Ya(e, t, n) {
	let { min: r = 0, max: i = 0 } = e, a = e.label();
	switch (e.kind) {
		case "bool": return typeof t == "boolean" ? { value: t } : { error: A("admin.gameSettings.errOnOff", { label: a }) };
		case "enum": return typeof t == "string" && ((e.options ?? []).includes(t) || t === n) ? { value: t } : { error: A("admin.gameSettings.errOption", { label: a }) };
		case "minutes": return typeof t == "number" && Number.isInteger(t) && t >= r * 6e10 && t <= i * 6e10 ? { value: t } : { error: A("admin.gameSettings.errMinutes", {
			label: a,
			min: r,
			max: i
		}) };
		case "int": return typeof t == "number" && Number.isInteger(t) && t >= r && t <= i ? { value: t } : { error: A("admin.gameSettings.errRange", {
			label: a,
			min: r,
			max: i
		}) };
		default: return typeof t != "number" || !Number.isFinite(t) || t < r || t > i ? { error: A("admin.gameSettings.errRange", {
			label: a,
			min: r,
			max: i
		}) } : { value: Math.round(t * 1e3) / 1e3 };
	}
}
var Xa = (e, t) => e.kind === "minutes" && typeof t == "number" ? Math.round(t / Ja) : t;
// src/admin/pages/AdminWorldPage.tsx
function Za() {
	let e = M();
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.world.title"),
			sub: e("admin.world.sub")
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ E("div", {
			className: "k-grid k-grid-2",
			children: [/* @__PURE__ */ T(Vr, {
				ops: ["time_get", "time_set"],
				what: e("admin.world.timeOfDay"),
				children: /* @__PURE__ */ T(eo, {})
			}), /* @__PURE__ */ T(Vr, {
				ops: "broadcast",
				what: e("admin.world.broadcast"),
				children: /* @__PURE__ */ T(to, {})
			})]
		}),
		/* @__PURE__ */ T(Vr, {
			ops: ["settings_get", "settings_set"],
			what: e("admin.world.liveSettings"),
			children: /* @__PURE__ */ T(no, {})
		})
	] });
}
var Qa = [
	6,
	9,
	12,
	19,
	22,
	0
];
function $a(e, t) {
	switch (e) {
		case 6: return t("admin.world.dawn");
		case 9: return t("admin.world.morning");
		case 12: return t("admin.world.noon");
		case 19: return t("admin.world.dusk");
		case 22: return t("admin.world.night");
		default: return t("admin.world.midnight");
	}
}
function eo() {
	let e = M(), t = Y(), n = c(), r = I("time_get", {}, { refetchInterval: 4e3 }), i = F("time_set", {
		onSuccess: (r) => {
			n.setQueriesData({ queryKey: [
				"admin",
				t,
				"time_get"
			] }, r), G(e("admin.world.timeNow", { clock: r.clock }));
		},
		onError: (e) => K(e)
	}), a = F("weather_reset", {
		onSuccess: () => G(e("admin.world.weatherReset")),
		onError: (e) => K(e)
	}), o = P("weather_reset"), [s, l] = x(""), [u, d] = x(!1), f = u ? { mode: "game" } : {}, p = () => {
		let t = /^(\d{1,2}):(\d{2})$/.exec(s.trim());
		if (!t || +t[1] > 23 || +t[2] > 59) return K({ message: e("admin.world.timeFormat") });
		i.mutate({
			clock: s.trim(),
			...f
		});
	}, m = r.data;
	return /* @__PURE__ */ T(U, {
		title: e("admin.world.timeOfDay"),
		sub: m ? e("admin.world.dayNight", {
			day: m.dayMinutes,
			night: m.nightMinutes
		}) : void 0,
		actions: m ? /* @__PURE__ */ T(V, {
			tone: m.day ? "warn" : "info",
			children: m.day ? e("admin.world.badgeDay") : e("admin.world.badgeNight")
		}) : null,
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			style: { gap: 14 },
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-time",
					children: [/* @__PURE__ */ T("span", {
						className: "ka-clock",
						children: m?.clock ?? "--:--"
					}), /* @__PURE__ */ T("div", {
						className: "k-row",
						children: Qa.map((t) => /* @__PURE__ */ T(L, {
							size: "s",
							variant: "secondary",
							disabled: i.isPending,
							onClick: () => i.mutate({
								hours: t,
								...f
							}),
							children: $a(t, e)
						}, t))
					})]
				}),
				m ? /* @__PURE__ */ T(cn, {
					"aria-label": e("admin.world.hour"),
					value: Math.round(m.hours * 4) / 4,
					min: 0,
					max: 23.75,
					step: .25,
					onCommit: (e) => i.mutate({
						hours: e,
						...f
					})
				}) : null,
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						/* @__PURE__ */ T(R, {
							"aria-label": e("admin.world.clock"),
							placeholder: "19:45",
							value: s,
							onChange: (e) => l(e.target.value),
							onKeyDown: (e) => e.key === "Enter" && p(),
							style: { width: 100 },
							mono: !0
						}),
						/* @__PURE__ */ T(L, {
							size: "s",
							loading: i.isPending,
							onClick: p,
							children: e("admin.world.setTime")
						}),
						/* @__PURE__ */ T(ln, {
							checked: u,
							onChange: d,
							label: e("admin.world.resetClock"),
							hint: e("admin.world.resetClockHint")
						})
					]
				}),
				m?.weather && o ? /* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [/* @__PURE__ */ T(L, {
						size: "s",
						loading: a.isPending,
						onClick: () => a.mutate({}),
						children: e("admin.world.resetWeather")
					}), /* @__PURE__ */ T("span", {
						className: "k-hint",
						children: e("admin.world.resetWeatherHint")
					})]
				}) : null
			]
		})
	});
}
function to() {
	let e = M(), [t, n] = x(""), r = F("broadcast", {
		onSuccess: (t) => {
			G(e("admin.world.sentTo", { count: t.delivered })), n("");
		},
		onError: (e) => K(e)
	}), i = () => {
		let n = t.trim();
		if (!n || n.length > 500) return K({ message: e("admin.world.messageRange") });
		r.mutate({ text: n });
	};
	return /* @__PURE__ */ T(U, {
		title: e("admin.world.broadcast"),
		sub: e("admin.world.broadcastSub"),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [/* @__PURE__ */ T(R, {
				"aria-label": e("admin.world.broadcastText"),
				value: t,
				maxLength: 500,
				placeholder: e("admin.world.broadcastPlaceholder"),
				onChange: (e) => n(e.target.value),
				onKeyDown: (e) => e.key === "Enter" && i()
			}), /* @__PURE__ */ E("div", {
				className: "k-row",
				children: [
					/* @__PURE__ */ E("span", {
						className: "k-hint",
						children: [t.length, " / 500"]
					}),
					/* @__PURE__ */ T("span", { className: "k-spacer" }),
					/* @__PURE__ */ T(L, {
						size: "s",
						loading: r.isPending,
						onClick: i,
						children: e("admin.world.sendEveryone")
					})
				]
			})]
		})
	});
}
function no() {
	let e = M(), t = Y(), n = c(), r = I("settings_get", {}, { staleTime: 1e4 }), [i, a] = x(/* @__PURE__ */ new Set()), [o, s] = x({}), l = F("settings_set"), u = F("settings_save", {
		onSuccess: () => G(e("admin.world.savedConfig")),
		onError: (e) => K(e)
	}), d = P("settings_save"), f = y(() => {
		let e = /* @__PURE__ */ new Map();
		for (let t of Ga) e.set(t.group, [...e.get(t.group) ?? [], t]);
		return [...e.entries()];
	}, []), p = r.data?.values ?? {}, m = (r, i) => {
		let o = Ya(r, r.kind === "minutes" && typeof i == "number" ? Math.round(i) * Ja : r.kind === "int" && typeof i == "number" ? Math.round(i) : i, p[r.key]);
		if (o.error) return s((e) => ({
			...e,
			[r.key]: o.error
		}));
		s(({ [r.key]: e, ...t }) => t), a((e) => new Set(e).add(r.key)), l.mutate({
			path: r.key,
			value: o.value
		}, {
			onSuccess: (i) => {
				n.setQueriesData({ queryKey: [
					"admin",
					t,
					"settings_get"
				] }, (e) => e && {
					...e,
					values: {
						...e.values,
						[r.key]: i.after
					}
				}), G(e("admin.world.settingNow", {
					label: r.label(),
					value: r.kind === "minutes" ? e("admin.world.minutesValue", { n: Math.round(Number(i.after) / Ja) }) : qa(String(i.after))
				}));
			},
			onError: (e) => s((t) => ({
				...t,
				[r.key]: e.message
			})),
			onSettled: () => a((e) => {
				let t = new Set(e);
				return t.delete(r.key), t;
			})
		});
	};
	return /* @__PURE__ */ E(U, {
		title: e("admin.world.settingsTitle"),
		sub: e("admin.world.settingsSub"),
		actions: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(L, {
			size: "s",
			variant: "ghost",
			onClick: () => Lt(n, t, ["settings_get"]),
			children: e("admin.world.reread")
		}), d ? /* @__PURE__ */ T(L, {
			size: "s",
			disabled: !r.data,
			loading: u.isPending,
			onClick: async () => {
				await q({
					title: e("admin.world.permanentTitle"),
					body: /* @__PURE__ */ T("p", { children: e("admin.world.permanentBody") }),
					ok: e("admin.world.saveToConfig")
				}) && u.mutate({});
			},
			children: e("admin.world.makePermanent")
		}) : null] }),
		children: [
			r.error ? /* @__PURE__ */ T(H, {
				tone: "warn",
				children: r.error.message
			}) : null,
			r.data && !r.data.write_supported ? /* @__PURE__ */ T(H, {
				tone: "warn",
				children: e("admin.world.readOnly")
			}) : null,
			!r.data && !r.error ? /* @__PURE__ */ T("div", {
				className: "k-muted",
				children: e("admin.world.readingSettings")
			}) : null,
			r.data ? /* @__PURE__ */ T("div", {
				className: "k-grid k-grid-2",
				style: { alignItems: "start" },
				children: f.map(([e, t]) => /* @__PURE__ */ E("div", {
					className: "k-stack",
					style: { gap: 4 },
					children: [/* @__PURE__ */ T("div", {
						className: "k-label",
						style: {
							textTransform: "uppercase",
							letterSpacing: ".08em",
							fontSize: 11
						},
						children: Ka(e)
					}), /* @__PURE__ */ T("div", {
						className: "ka-setting-group",
						children: t.map((e) => /* @__PURE__ */ T(ro, {
							spec: e,
							value: p[e.key],
							busy: i.has(e.key),
							error: o[e.key],
							onChange: (t) => m(e, t)
						}, e.key))
					})]
				}, e))
			}) : null
		]
	});
}
function ro({ spec: e, value: t, busy: n, error: r, onChange: i }) {
	let a = M(), o = e.label(), s = /* @__PURE__ */ E("div", {
		className: "ka-setting-lbl",
		children: [/* @__PURE__ */ T("b", { children: o }), r ? /* @__PURE__ */ T("small", {
			className: "k-error-text",
			children: r
		}) : e.hint ? /* @__PURE__ */ T("small", { children: e.hint() }) : null]
	});
	if (t === void 0) return /* @__PURE__ */ E("div", {
		className: "ka-setting",
		children: [s, /* @__PURE__ */ T("span", {
			className: "k-muted",
			children: a("admin.world.notReported")
		})]
	});
	let c;
	if (e.kind === "bool") c = /* @__PURE__ */ T(ln, {
		checked: t === !0,
		onChange: i,
		disabled: n,
		label: a(t ? "admin.world.on" : "admin.world.off")
	});
	else if (e.kind === "enum") {
		let r = (e.options ?? []).includes(String(t)) ? e.options : [String(t), ...e.options ?? []];
		c = /* @__PURE__ */ T(z, {
			"aria-label": o,
			value: String(t),
			onChange: i,
			disabled: n,
			options: r.map((e) => ({
				value: e,
				label: qa(e)
			}))
		});
	} else {
		let r = Number(Xa(e, t)), a = e.kind === "factor" ? .05 : 1;
		c = /* @__PURE__ */ T(cn, {
			"aria-label": o,
			value: r,
			min: e.min,
			max: e.max,
			step: a,
			integer: e.kind !== "factor",
			onCommit: i,
			disabled: n
		});
	}
	return /* @__PURE__ */ E("div", {
		className: "ka-setting" + (n ? " busy" : ""),
		children: [s, c]
	});
}
var io = Br(Za);
// src/admin/pages/AdminCombatPage.tsx
function ao(e) {
	switch (e) {
		case "Game": return A("admin.combat.originGame");
		case "Kiln": return A("admin.combat.originKiln");
		case "Other": return A("admin.combat.originOther");
		default: return e;
	}
}
function oo() {
	let e = M(), t = Ft().state?.players ?? [];
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.combat.title"),
			sub: e("admin.combat.sub")
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ E(Vr, {
			ops: [
				"health",
				"heal",
				"god",
				"kill"
			],
			what: e("admin.combat.what"),
			children: [/* @__PURE__ */ T(U, {
				title: e("admin.combat.inWorld"),
				sub: e("admin.combat.inWorldSub"),
				children: t.length ? /* @__PURE__ */ T("div", {
					className: "ka-list",
					children: t.map((e) => /* @__PURE__ */ T(so, { p: e }, e.entity))
				}) : /* @__PURE__ */ T(W, {
					title: e("admin.combat.nobody"),
					hint: e("admin.combat.nobodyHint")
				})
			}), /* @__PURE__ */ E("div", {
				className: "k-grid k-grid-2",
				style: { alignItems: "start" },
				children: [/* @__PURE__ */ T(co, {}), /* @__PURE__ */ T(lo, { players: t })]
			})]
		}),
		/* @__PURE__ */ T(uo, {}),
		/* @__PURE__ */ T("div", {
			className: "k-hint",
			children: e("admin.combat.footHint")
		})
	] });
}
function so({ p: e }) {
	let t = M(), n = J(e), r = F("heal", {
		onSuccess: (e) => G(t("admin.combat.healed", {
			name: n,
			n: e.healed ?? 0
		})),
		onError: (e) => K(e)
	}), i = F("god", {
		onSuccess: (e) => G(e.god ? t("admin.players.godOn", { name: n }) : t("admin.players.godOff", { name: n })),
		onError: (e) => K(e)
	}), a = F("kill", {
		onSuccess: (e) => e.killed ? G(t("admin.players.killed", { name: n })) : K({ message: e.state ? `${n}: ${e.state}` : t("admin.combat.noEffect", { name: n }) }),
		onError: (e) => K(e)
	}), o = e.healthMax ? Math.round((e.health ?? 0) / e.healthMax * 100) : null, s = e.healthMax && (e.health ?? 0) <= 0, c = r.isPending || i.isPending || a.isPending;
	return /* @__PURE__ */ E("div", {
		className: "ka-row-item",
		style: { gap: 16 },
		children: [
			/* @__PURE__ */ E("span", {
				className: "ka-person",
				style: { minWidth: 220 },
				children: [/* @__PURE__ */ T(Mr, {
					name: n,
					online: !0
				}), /* @__PURE__ */ E("span", { children: [
					/* @__PURE__ */ T("span", {
						className: "ka-person-name",
						children: n
					}),
					" ",
					e.god ? /* @__PURE__ */ T(V, {
						tone: "ember",
						children: t("admin.combat.godBadge")
					}) : null,
					" ",
					s ? /* @__PURE__ */ T(V, {
						tone: "err",
						children: t("admin.combat.dead")
					}) : null,
					/* @__PURE__ */ T("div", {
						className: "ka-person-meta",
						children: o == null ? t("admin.combat.healthUnknown") : t("admin.ui.healthTitle", {
							health: String(e.health),
							max: e.healthMax
						})
					})
				] })]
			}),
			/* @__PURE__ */ T(Or, {
				health: e.health,
				max: e.healthMax
			}),
			/* @__PURE__ */ T("span", { className: "k-spacer" }),
			/* @__PURE__ */ T(L, {
				size: "s",
				disabled: c || o === 100,
				title: o === 100 ? t("admin.combat.fullHealth") : void 0,
				onClick: () => r.mutate({ entity: e.entity }),
				children: t("admin.players.healFull")
			}),
			/* @__PURE__ */ T(ln, {
				checked: !!e.god,
				disabled: c,
				label: t("admin.players.godMode"),
				onChange: (t) => i.mutate({
					entity: e.entity,
					on: t
				})
			}),
			/* @__PURE__ */ T(L, {
				size: "s",
				variant: "danger",
				disabled: c,
				onClick: async () => {
					await q({
						title: t("admin.players.killTitle", { name: n }),
						body: /* @__PURE__ */ T("p", { children: t("admin.combat.killBody", { name: n }) }),
						typed: n,
						ok: t("admin.players.kill")
					}) && a.mutate({ entity: e.entity });
				},
				children: t("admin.combat.killMore")
			})
		]
	});
}
function co() {
	let e = M(), [t, n] = x(null), [r, i] = x(50), a = P("damage"), o = I("health", { entity: t ?? 0 }, {
		enabled: t != null && t >= 1,
		refetchInterval: 3e3
	}), s = F("damage", {
		onSuccess: (t) => {
			G(t.killed ? e("admin.combat.hitKilled", {
				state: t.state,
				health: t.health
			}) : e("admin.combat.hit", {
				state: t.state,
				health: t.health
			})), o.refetch();
		},
		onError: (e) => K(e)
	}), c = F("kill", {
		onSuccess: (n) => {
			n.killed ? G(e("admin.combat.entityKilled", { entity: String(t) })) : K({ message: n.state }), o.refetch();
		},
		onError: (e) => K(e)
	}), l = o.data;
	return /* @__PURE__ */ T(U, {
		title: e("admin.combat.anyEntity"),
		sub: e("admin.combat.anyEntitySub"),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [/* @__PURE__ */ T(B, {
						"aria-label": e("admin.combat.entityId"),
						placeholder: e("admin.combat.entityIdPlaceholder"),
						value: t,
						integer: !0,
						min: 1,
						onCommit: n,
						style: { width: 140 }
					}), t ? /* @__PURE__ */ T(L, {
						size: "s",
						variant: "ghost",
						onClick: () => o.refetch(),
						children: e("admin.combat.readHealth")
					}) : null]
				}),
				o.error ? /* @__PURE__ */ T("div", {
					className: "k-error-text",
					children: o.error.message
				}) : null,
				l ? /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						/* @__PURE__ */ E("span", {
							className: "k-mono",
							children: ["#", l.entity]
						}),
						/* @__PURE__ */ T(Or, {
							health: l.health,
							max: l.healthMax
						}),
						/* @__PURE__ */ E("span", {
							className: "k-hint",
							children: [
								l.health,
								" / ",
								l.healthMax
							]
						}),
						l.dead ? /* @__PURE__ */ T(V, {
							tone: "err",
							children: e("admin.combat.dead")
						}) : null,
						l.god ? /* @__PURE__ */ T(V, {
							tone: "ember",
							children: e("admin.combat.invulnerable")
						}) : null
					]
				}), /* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [a ? /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(B, {
						"aria-label": e("admin.combat.damage"),
						value: r,
						integer: !0,
						min: 1,
						max: 1e6,
						onCommit: i,
						style: { width: 100 }
					}), /* @__PURE__ */ T(L, {
						size: "s",
						loading: s.isPending,
						onClick: () => s.mutate({
							entity: l.entity,
							amount: r
						}),
						children: e("admin.combat.damage")
					})] }) : null, /* @__PURE__ */ T(L, {
						size: "s",
						variant: "danger",
						loading: c.isPending,
						onClick: async () => {
							await q({
								title: e("admin.combat.killEntityTitle", { entity: l.entity }),
								body: /* @__PURE__ */ T("p", { children: e("admin.combat.killEntityBody") }),
								typed: String(l.entity),
								ok: e("admin.players.kill")
							}) && c.mutate({ entity: l.entity });
						},
						children: e("admin.combat.killMore")
					})]
				})] }) : null
			]
		})
	});
}
function lo({ players: e }) {
	let t = M(), [n, r] = x(""), i = n || (e[0] ? `p:${e[0].entity}` : "coords"), [a, o] = x(Xr(null)), [s, c] = x(80), [l, u] = x(null), d = I("enemies", l ?? {}, {
		enabled: !!l,
		refetchInterval: 5e3
	}), f = () => {
		let n = null;
		if (i.startsWith("p:")) n = e.find((e) => e.entity === Number(i.slice(2)))?.pos ?? null;
		else {
			let e = Qr(a);
			if (e.error) return K({ message: e.error });
			n = e.pos;
		}
		if (!n) return K({ message: t("admin.zones.noPlayerPos") });
		if (s < 1 || s > 500) return K({ message: t("admin.combat.radiusRange") });
		u({
			near: n,
			radius: s
		});
	}, p = d.data?.near ?? [];
	return /* @__PURE__ */ T(U, {
		title: t("admin.combat.nearby"),
		sub: d.data && l ? t("admin.combat.nearbySub", {
			n: p.length,
			radius: l.radius,
			total: d.data.total
		}) : t("admin.combat.nearbyHint"),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						/* @__PURE__ */ T(z, {
							"aria-label": t("admin.combat.around"),
							value: i,
							onChange: r,
							options: [...e.map((e) => ({
								value: `p:${e.entity}`,
								label: t("admin.combat.aroundPlayer", { name: J(e) })
							})), {
								value: "coords",
								label: t("admin.combat.aroundCoords")
							}]
						}),
						i === "coords" ? /* @__PURE__ */ T($r, {
							value: a,
							onChange: o
						}) : null,
						/* @__PURE__ */ T(B, {
							"aria-label": t("admin.zones.colRadius"),
							value: s,
							integer: !0,
							min: 1,
							max: 500,
							onCommit: c,
							style: { width: 80 }
						}),
						/* @__PURE__ */ T(L, {
							size: "s",
							loading: d.isFetching && !d.data,
							onClick: f,
							children: t("admin.combat.scan")
						})
					]
				}),
				d.error ? /* @__PURE__ */ T("div", {
					className: "k-error-text",
					children: d.error.message
				}) : null,
				l && d.data ? /* @__PURE__ */ T(Gn, {
					"aria-label": t("admin.combat.nearby"),
					rows: p,
					rowKey: (e) => String(e.entity),
					maxHeight: 320,
					columns: [
						{
							key: "e",
							header: t("admin.combat.colEntity"),
							width: "90px",
							render: (e) => /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: e.entity
							})
						},
						{
							key: "n",
							header: t("admin.spawn.creature"),
							render: (e) => e.name ?? e.guid ?? "?"
						},
						{
							key: "p",
							header: t("admin.items.where"),
							width: "150px",
							render: (e) => /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: Rr(e.pos)
							})
						}
					],
					empty: /* @__PURE__ */ T(W, { title: t("admin.combat.noCreatures") })
				}) : null
			]
		})
	});
}
function uo() {
	let e = M(), t = [...Jt(Y()).deaths].reverse().slice(0, 200);
	return /* @__PURE__ */ T(U, {
		title: e("admin.combat.deaths"),
		sub: e("admin.combat.deathsSub"),
		flush: !0,
		children: /* @__PURE__ */ T(Gn, {
			"aria-label": e("admin.combat.deaths"),
			rows: t,
			rowKey: (e) => e.key,
			columns: [
				{
					key: "t",
					header: e("admin.saves.colWhen"),
					width: "130px",
					render: (e) => /* @__PURE__ */ T("span", {
						title: Lr(e.time),
						children: Fr(e.time)
					}),
					sort: (e, t) => e.time - t.time
				},
				{
					key: "v",
					header: e("admin.players.colPlayer"),
					width: "180px",
					render: (t) => t.victimName ?? e("admin.combat.playerLower", { entity: t.entity })
				},
				{
					key: "k",
					header: e("admin.combat.killedBy"),
					render: (t) => t.killerKind === "none" || !t.killer && !t.killerName ? /* @__PURE__ */ T("span", {
						className: "k-muted",
						children: e("admin.combat.nobodyKiller", { origin: ao(t.origin) ?? "?" })
					}) : /* @__PURE__ */ E("span", {
						className: "ka-death-killer",
						children: [
							t.killerName ?? e("admin.combat.entityLower", { entity: String(t.killer) }),
							" ",
							t.killerKind ? /* @__PURE__ */ T(V, {
								tone: t.killerKind === "player" ? "warn" : "idle",
								children: t.killerKind
							}) : null,
							t.origin && t.origin !== "Game" ? /* @__PURE__ */ T("span", {
								className: "k-muted",
								children: ao(t.origin)
							}) : null
						]
					})
				},
				{
					key: "p",
					header: e("admin.items.where"),
					width: "170px",
					render: (e) => /* @__PURE__ */ T("span", {
						className: "k-mono",
						children: Rr(e.pos)
					})
				}
			],
			empty: /* @__PURE__ */ T(W, {
				title: e("admin.map.noDeaths"),
				hint: e("admin.combat.noDeathsHint")
			})
		})
	});
}
var fo = Br(oo), po = () => [{
	value: "",
	label: A("admin.creatures.anyKind")
}, ...[
	"enemy",
	"wildlife",
	"animal",
	"villager"
].map((e) => ({
	value: e,
	label: e
}))], mo = [
	"level",
	"faction",
	"scale",
	"xp",
	"speed",
	"damage",
	"home",
	"loot-slots",
	"loot-off",
	"feature-on",
	"feature-off",
	"arsenal",
	"loot-labels",
	"resist:fire",
	"resist:physical",
	"resist:magical"
], ho = [
	"stay",
	"heal",
	"moveto",
	"attack",
	"guard",
	"stopguard"
];
function go() {
	let e = M(), t = Hr(), [n, r] = x(null), i = I("creature_status", {}, { refetchInterval: 1e4 }).data;
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.creatures.title"),
			sub: i ? e("admin.creatures.sub", {
				templates: i.catalog,
				creatures: e("admin.creatures.creatureCount", { count: i.entities }),
				changes: e("admin.creatures.changeCount", { count: i.layers })
			}) : e("admin.creatures.subNone")
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ T(Vr, {
			ops: [
				"creature_status",
				"creature_catalog",
				"creature_near",
				"creature_inspect"
			],
			what: e("admin.creatures.what"),
			children: /* @__PURE__ */ E("div", {
				className: "ka-split",
				children: [/* @__PURE__ */ E("div", {
					className: "k-stack",
					style: {
						gap: 22,
						minWidth: 0
					},
					children: [/* @__PURE__ */ T(vo, {
						players: t,
						onPick: r,
						picked: n
					}), /* @__PURE__ */ T(bo, {
						players: t,
						onSpawned: r
					})]
				}), /* @__PURE__ */ E("div", {
					className: "k-stack",
					style: {
						gap: 12,
						minWidth: 0
					},
					children: [/* @__PURE__ */ T(yo, {
						entity: n,
						setEntity: r,
						players: t
					}), /* @__PURE__ */ T("p", {
						className: "k-hint",
						children: e("admin.creatures.footHint")
					})]
				})]
			})
		})
	] });
}
function _o(e) {
	let t = M(), [n, r] = x(""), i = n || (e[0] ? `p:${e[0].entity}` : "coords"), [a, o] = x(Xr(null)), s = () => {
		if (i.startsWith("p:")) return e.find((e) => e.entity === Number(i.slice(2)))?.pos ?? null;
		let t = Qr(a);
		return t.error ? (K({ message: t.error }), null) : t.pos;
	};
	return {
		ui: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(z, {
			"aria-label": t("admin.items.where"),
			value: i,
			onChange: r,
			options: [...e.map((e) => ({
				value: `p:${e.entity}`,
				label: t("admin.items.atPlayer", { name: J(e) })
			})), {
				value: "coords",
				label: t("admin.items.atCoords")
			}]
		}), i === "coords" ? /* @__PURE__ */ T($r, {
			value: a,
			onChange: o
		}) : null] }),
		pos: s
	};
}
function vo({ players: e, onPick: t, picked: n }) {
	let r = M(), i = _o(e), [a, o] = x(60), [s, c] = x(null), l = I("creature_near", s ?? {}, {
		enabled: !!s,
		refetchInterval: 5e3
	}), u = () => {
		let e = i.pos();
		if (e) {
			if (a < 1 || a > 2e3) return K({ message: r("admin.zones.radiusRange") });
			c({
				x: e.x,
				y: e.y,
				z: e.z,
				radius: a
			});
		}
	}, d = l.data ?? [];
	return /* @__PURE__ */ T(U, {
		title: r("admin.creatures.nearby"),
		sub: s ? r("admin.creatures.nearbySub", {
			n: d.length,
			radius: s.radius
		}) : r("admin.creatures.nearbyHint"),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						i.ui,
						/* @__PURE__ */ T(B, {
							"aria-label": r("admin.zones.colRadius"),
							value: a,
							integer: !0,
							min: 1,
							max: 2e3,
							onCommit: o,
							style: { width: 90 }
						}),
						/* @__PURE__ */ T(L, {
							size: "s",
							loading: l.isFetching && !l.data,
							onClick: u,
							children: r("admin.combat.scan")
						})
					]
				}),
				l.error ? /* @__PURE__ */ T("div", {
					className: "k-error-text",
					children: l.error.message
				}) : null,
				s ? /* @__PURE__ */ T(Gn, {
					"aria-label": r("admin.combat.nearby"),
					rows: d,
					rowKey: (e) => String(e.entity),
					maxHeight: 340,
					selectedKey: n == null ? null : String(n),
					onRowClick: (e) => t(e.entity),
					empty: /* @__PURE__ */ T(W, {
						title: r("admin.creatures.nothingHere"),
						hint: r("admin.creatures.nothingHereHint")
					}),
					columns: [
						{
							key: "e",
							header: r("admin.combat.colEntity"),
							width: "80px",
							render: (e) => /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: e.entity
							})
						},
						{
							key: "n",
							header: r("admin.spawn.creature"),
							render: (e) => e.templateName
						},
						{
							key: "l",
							header: r("admin.players.level"),
							width: "60px",
							align: "right",
							render: (e) => e.level
						},
						{
							key: "s",
							header: r("admin.players.colState"),
							width: "110px",
							render: (e) => e.dead ? /* @__PURE__ */ T(V, {
								tone: "err",
								children: r("admin.combat.dead")
							}) : e.state
						},
						{
							key: "h",
							header: r("admin.players.colHealth"),
							width: "120px",
							render: (e) => /* @__PURE__ */ T(Or, {
								health: e.health,
								max: e.healthMax
							})
						}
					]
				}) : null
			]
		})
	});
}
function yo({ entity: e, setEntity: t, players: n }) {
	let r = M(), i = I("creature_inspect", { entity: e ?? 0 }, {
		enabled: e != null && e >= 1,
		refetchInterval: 4e3
	}), a = P("creature_set"), o = P("creature_command"), [s, c] = x("level"), [l, u] = x(""), [d, f] = x("stay"), [p, m] = x(null), h = _o(n), g = F("creature_set", {
		onSuccess: () => {
			G(r("admin.creatures.fieldSet", { field: s })), i.refetch();
		},
		onError: (e) => K(e)
	}), _ = F("creature_reset", {
		onSuccess: (e) => {
			G(r("admin.creatures.resetDone", { n: e.reset })), i.refetch();
		},
		onError: (e) => K(e)
	}), v = F("creature_command", {
		onSuccess: () => {
			G(r("admin.creatures.actionSent", { action: d })), i.refetch();
		},
		onError: (e) => K(e)
	}), y = i.data;
	return /* @__PURE__ */ T(U, {
		title: r("admin.creatures.inspect"),
		sub: r("admin.creatures.inspectSub"),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [/* @__PURE__ */ T(B, {
						"aria-label": r("admin.combat.entityId"),
						placeholder: r("admin.combat.entityIdPlaceholder"),
						value: e,
						integer: !0,
						min: 1,
						onCommit: (e) => t(e),
						style: { width: 140 }
					}), y ? /* @__PURE__ */ T(L, {
						size: "s",
						variant: "ghost",
						onClick: () => i.refetch(),
						children: r("common.refresh")
					}) : null]
				}),
				i.error ? /* @__PURE__ */ T("div", {
					className: "k-error-text",
					children: i.error.message
				}) : null,
				y ? /* @__PURE__ */ E(w, { children: [
					/* @__PURE__ */ E("div", {
						className: "ka-inline",
						children: [
							/* @__PURE__ */ T("b", { children: y.templateName }),
							" ",
							/* @__PURE__ */ T(V, { children: y.kind }),
							" ",
							/* @__PURE__ */ T(V, {
								tone: "info",
								children: y.faction
							}),
							" ",
							y.layers ? /* @__PURE__ */ T(V, {
								tone: "ember",
								children: r("admin.creatures.layers", { n: y.layers })
							}) : null,
							/* @__PURE__ */ T(Or, {
								health: y.health,
								max: y.healthMax
							})
						]
					}),
					/* @__PURE__ */ T(Hn, { rows: [
						[r("admin.players.colState"), y.dead ? r("admin.combat.dead") : y.state],
						[r("admin.players.level"), r("admin.creatures.levelUpTo", {
							level: y.level,
							max: y.levelMax
						})],
						[r("admin.creatures.kvStats"), `${y.scale} · ${y.xp} · ${y.speed} · ${y.damage}`],
						[r("admin.creatures.kvHome"), /* @__PURE__ */ T("span", {
							className: "k-mono",
							children: Rr(y.home)
						})],
						[r("admin.creatures.kvTarget"), y.target ?? "-"],
						[r("admin.creatures.kvFeaturesOff"), y.features.disabled],
						[r("admin.creatures.kvLoot"), y.loot.disabled ? r("admin.map.off") : r("admin.creatures.lootSlots", {
							min: y.loot.min,
							max: y.loot.max
						})]
					] }),
					a ? /* @__PURE__ */ E("div", {
						className: "ka-inline",
						children: [
							/* @__PURE__ */ T(z, {
								"aria-label": r("admin.creatures.field"),
								value: s,
								onChange: c,
								options: mo
							}),
							s === "home" ? h.ui : /* @__PURE__ */ T(R, {
								"aria-label": r("admin.gameData.value"),
								value: l,
								placeholder: s.startsWith("feature") ? "Patrol, Roaming" : r("admin.creatures.valuePlaceholder"),
								onChange: (e) => u(e.target.value),
								style: { width: 160 }
							}),
							/* @__PURE__ */ T(L, {
								size: "s",
								loading: g.isPending,
								onClick: () => {
									if (!y) return;
									let e = l.trim();
									if (s === "home") {
										let t = h.pos();
										if (!t) return;
										e = t;
									} else if (e === "") return K({ message: r("admin.creatures.enterValue") });
									else /^-?\d+(\.\d+)?$/.test(e) ? e = Number(e) : (e === "true" || e === "false") && (e = e === "true");
									g.mutate({
										entity: y.entity,
										field: s,
										value: e
									});
								},
								children: r("admin.gameData.set")
							}),
							/* @__PURE__ */ T(L, {
								size: "s",
								variant: "ghost",
								loading: _.isPending,
								disabled: !y.layers,
								onClick: () => _.mutate({ entity: y.entity }),
								children: r("common.reset")
							})
						]
					}) : null,
					o ? /* @__PURE__ */ E("div", {
						className: "ka-inline",
						children: [
							/* @__PURE__ */ T(z, {
								"aria-label": r("admin.creatures.command"),
								value: d,
								onChange: f,
								options: ho
							}),
							d === "moveto" ? h.ui : null,
							d === "attack" || d === "guard" || d === "stopguard" ? /* @__PURE__ */ T(B, {
								"aria-label": r("admin.creatures.targetEntity"),
								placeholder: r("admin.creatures.targetPlaceholder"),
								value: p,
								integer: !0,
								min: 1,
								onCommit: m,
								style: { width: 130 }
							}) : null,
							/* @__PURE__ */ T(L, {
								size: "s",
								loading: v.isPending,
								onClick: async () => {
									if (!y) return;
									let e = {
										entity: y.entity,
										action: d
									};
									if (d === "moveto") {
										let t = h.pos();
										if (!t) return;
										Object.assign(e, t);
									}
									if (d === "attack" || d === "guard" || d === "stopguard") {
										if (!p) return K({ message: r("admin.creatures.giveTarget") });
										if (e.target = p, d === "attack" && !await q({
											title: r("admin.creatures.attackTitle", {
												name: y.templateName,
												target: p
											}),
											body: /* @__PURE__ */ T("p", { children: r("admin.creatures.attackBody") }),
											ok: r("admin.creatures.attack")
										})) return;
									}
									v.mutate(e);
								},
								children: r("admin.players.send")
							})
						]
					}) : null
				] }) : e ? null : /* @__PURE__ */ T(W, {
					title: r("admin.creatures.noneTitle"),
					hint: r("admin.creatures.noneHint")
				})
			]
		})
	});
}
function bo({ players: e, onSpawned: t }) {
	let n = M(), [r, i] = x(""), [a, o] = x(""), [s, c] = x(1), l = P("creature_spawn"), u = _o(e), d = I("creature_catalog", {
		...r.trim() ? { filter: r.trim() } : {},
		...a ? { kind: a } : {}
	}, { staleTime: 6e4 }), f = F("creature_spawn", {
		onSuccess: (e) => {
			G(n("admin.creatures.spawned", {
				n: e.entities.length,
				list: e.entities.join(", ")
			})), e.entities[0] && t(e.entities[0]);
		},
		onError: (e) => K(e)
	}), p = (e) => {
		let t = u.pos();
		t && f.mutate({
			what: e.name,
			count: s,
			...t
		});
	}, m = d.data ?? [];
	return /* @__PURE__ */ T(U, {
		title: n("admin.creatures.catalog"),
		sub: d.data ? n("admin.creatures.templates", { n: m.length }) : n("admin.creatures.catalogHint"),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						/* @__PURE__ */ T(R, {
							"aria-label": n("admin.gameData.search"),
							placeholder: n("admin.creatures.searchNames"),
							value: r,
							onChange: (e) => i(e.target.value),
							style: { width: 220 }
						}),
						/* @__PURE__ */ T(z, {
							"aria-label": n("admin.items.colKind"),
							value: a,
							onChange: o,
							options: po()
						}),
						l ? /* @__PURE__ */ E(w, { children: [
							/* @__PURE__ */ T("span", {
								className: "k-hint",
								children: n("admin.spawn.title")
							}),
							/* @__PURE__ */ T(B, {
								"aria-label": n("admin.creatures.count"),
								value: s,
								integer: !0,
								min: 1,
								max: 50,
								onCommit: c,
								style: { width: 70 }
							}),
							u.ui
						] }) : null
					]
				}),
				d.error ? /* @__PURE__ */ T("div", {
					className: "k-error-text",
					children: d.error.message
				}) : null,
				/* @__PURE__ */ T(Gn, {
					"aria-label": n("admin.creatures.catalogTable"),
					rows: m,
					rowKey: (e) => e.guid,
					maxHeight: 380,
					columns: [
						{
							key: "n",
							header: n("admin.spawn.colTemplate"),
							render: (e) => /* @__PURE__ */ E("span", { children: [
								e.display || e.name,
								" ",
								e.display ? /* @__PURE__ */ T("span", {
									className: "k-hint k-mono",
									children: e.name
								}) : null
							] }),
							sort: (e, t) => e.name.localeCompare(t.name)
						},
						{
							key: "k",
							header: n("admin.items.colKind"),
							width: "90px",
							render: (e) => e.kind
						},
						{
							key: "f",
							header: n("admin.creatures.faction"),
							width: "110px",
							render: (e) => e.faction
						},
						{
							key: "l",
							header: n("admin.players.level"),
							width: "80px",
							render: (e) => `${e.level}-${e.levelMax}`
						},
						{
							key: "b",
							header: "",
							width: "70px",
							render: (e) => e.boss ? /* @__PURE__ */ T(V, {
								tone: "ember",
								children: n("admin.creatures.boss")
							}) : null
						},
						...l ? [{
							key: "s",
							header: "",
							width: "80px",
							render: (e) => /* @__PURE__ */ T(L, {
								size: "s",
								loading: f.isPending,
								onClick: () => p(e),
								children: n("admin.spawn.title")
							})
						}] : []
					]
				})
			]
		})
	});
}
var xo = Br(go), So = [
	"templates",
	"arsenals",
	"sequences",
	"buffs",
	"perks",
	"skills",
	"items"
];
function Co(e) {
	try {
		return JSON.parse(e);
	} catch {
		return e;
	}
}
var wo = (e) => typeof e == "string" ? e : JSON.stringify(e);
function To() {
	let e = M(), t = I("gamedata_status", {}, { refetchInterval: 15e3 }), [n, r] = x("templates"), [i, a] = x(""), [o, s] = x(null), [c, l] = x(null), u = I("gamedata_search", o ? {
		...o,
		limit: 200
	} : {}, { enabled: !!o }), d = t.data?.sections;
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.gameData.title"),
			sub: d ? So.filter((e) => d[e]).map((t) => d[t].changed ? e("admin.gameData.sectionChanged", {
				section: t,
				count: d[t].count,
				changed: d[t].changed
			}) : e("admin.gameData.section", {
				section: t,
				count: d[t].count
			})).join(" · ") : e("admin.gameData.sub")
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ T(Vr, {
			ops: [
				"gamedata_status",
				"gamedata_search",
				"gamedata_get"
			],
			what: e("admin.gameData.title"),
			children: /* @__PURE__ */ E("div", {
				className: "k-grid k-grid-2",
				style: { alignItems: "start" },
				children: [/* @__PURE__ */ T(U, {
					title: e("admin.gameData.search"),
					sub: e("admin.gameData.searchSub"),
					children: /* @__PURE__ */ E("div", {
						className: "k-stack",
						children: [
							/* @__PURE__ */ E("div", {
								className: "ka-inline",
								children: [
									/* @__PURE__ */ T(z, {
										"aria-label": e("admin.gameData.sectionLabel"),
										value: n,
										onChange: (e) => {
											r(e), l(null);
										},
										options: So
									}),
									/* @__PURE__ */ T(R, {
										"aria-label": e("admin.gameData.search"),
										placeholder: e("admin.gameData.searchPlaceholder"),
										value: i,
										onChange: (e) => a(e.target.value),
										onKeyDown: (e) => {
											e.key === "Enter" && s({
												section: n,
												q: i.trim()
											});
										},
										style: { width: 220 }
									}),
									/* @__PURE__ */ T(L, {
										size: "s",
										loading: u.isFetching && !u.data,
										onClick: () => s({
											section: n,
											q: i.trim()
										}),
										children: e("admin.gameData.search")
									})
								]
							}),
							u.error ? /* @__PURE__ */ T("div", {
								className: "k-error-text",
								children: u.error.message
							}) : null,
							o ? /* @__PURE__ */ T(Gn, {
								"aria-label": e("admin.gameData.entries"),
								rows: u.data ?? [],
								rowKey: (e) => e.id,
								maxHeight: 420,
								selectedKey: c?.id ?? null,
								onRowClick: l,
								empty: /* @__PURE__ */ T(W, {
									title: e("admin.gameData.nothingFound"),
									hint: e("admin.gameData.nothingFoundHint")
								}),
								columns: [{
									key: "n",
									header: e("admin.gameData.entry"),
									render: (t) => /* @__PURE__ */ E("span", { children: [
										t.name,
										" ",
										t.modified ? /* @__PURE__ */ T(V, {
											tone: "ember",
											children: e("admin.gameData.changed")
										}) : null,
										" ",
										t.clone ? /* @__PURE__ */ T(V, {
											tone: "info",
											children: e("admin.gameData.clone")
										}) : null
									] })
								}, {
									key: "i",
									header: e("admin.items.colId"),
									width: "110px",
									render: (e) => /* @__PURE__ */ T("span", {
										className: "k-mono",
										children: e.id
									})
								}]
							}) : null
						]
					})
				}), c ? /* @__PURE__ */ T(Eo, { hit: c }, c.section + c.id) : /* @__PURE__ */ T(U, {
					title: e("admin.gameData.entry"),
					children: /* @__PURE__ */ T(W, {
						title: e("admin.gameData.noEntry"),
						hint: e("admin.gameData.noEntryHint")
					})
				})]
			})
		}),
		/* @__PURE__ */ T("div", {
			className: "k-hint",
			children: e("admin.gameData.valuesHint")
		})
	] });
}
function Eo({ hit: e }) {
	let t = M(), n = e.section === "templates", r = P("template_fields"), i = I("template_fields", { entry: e.name }, { enabled: n && r }), a = I("gamedata_get", {
		section: e.section,
		entry: e.name,
		path: ""
	}, { enabled: !n || !r }), [o, s] = x(""), [c, l] = x(""), [u, d] = x(""), f = I("gamedata_get", {
		section: e.section,
		entry: e.name,
		path: o
	}, { enabled: o.trim().length > 0 }), p = () => {
		f.refetch(), i.refetch(), a.refetch();
	}, m = F("gamedata_set", {
		onSuccess: (e) => {
			G(t("admin.gameData.setDone", { n: e.applied })), p();
		},
		onError: (e) => K(e)
	}), h = F("gamedata_reset", {
		onSuccess: (e) => {
			G(e.reset ? t("common.reset") : t("admin.gameData.nothingToReset")), p();
		},
		onError: (e) => K(e)
	}), g = (i.data ?? []).filter((e) => !u || e.path.toLowerCase().includes(u.toLowerCase()));
	return /* @__PURE__ */ T(U, {
		title: e.name,
		sub: `${e.section} · ${e.id}${e.guid ? ` · ${e.guid}` : ""}`,
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						/* @__PURE__ */ T(R, {
							"aria-label": t("admin.gameData.path"),
							mono: !0,
							placeholder: t("admin.gameData.pathPlaceholder"),
							value: o,
							onChange: (e) => s(e.target.value),
							style: { width: 260 }
						}),
						/* @__PURE__ */ T(R, {
							"aria-label": t("admin.gameData.value"),
							mono: !0,
							placeholder: t("admin.gameData.valuePlaceholder"),
							value: c,
							onChange: (e) => l(e.target.value),
							style: { width: 170 }
						}),
						/* @__PURE__ */ T(L, {
							size: "s",
							variant: "primary",
							disabled: !o.trim() || !c.trim(),
							loading: m.isPending,
							onClick: () => m.mutate({
								section: e.section,
								entry: e.name,
								path: o.trim(),
								value: Co(c.trim())
							}),
							children: t("admin.gameData.set")
						}),
						/* @__PURE__ */ T(L, {
							size: "s",
							variant: "ghost",
							disabled: !o.trim(),
							loading: h.isPending,
							onClick: () => h.mutate({
								section: e.section,
								entry: e.name,
								path: o.trim()
							}),
							children: t("common.reset")
						})
					]
				}),
				f.data && o.trim() ? /* @__PURE__ */ T("div", {
					className: "k-hint k-mono",
					children: f.data.writable ? t("admin.gameData.fieldInfo", {
						type: f.data.type,
						value: wo(f.data.value),
						original: wo(f.data.original)
					}) : t("admin.gameData.fieldInfoRo", {
						type: f.data.type,
						value: wo(f.data.value),
						original: wo(f.data.original)
					})
				}) : f.error ? /* @__PURE__ */ T("div", {
					className: "k-error-text",
					children: f.error.message
				}) : null,
				n && r ? /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(R, {
					"aria-label": t("admin.gameData.filterFields"),
					placeholder: t("admin.gameData.filterFieldsPlaceholder"),
					value: u,
					onChange: (e) => d(e.target.value)
				}), /* @__PURE__ */ T(Gn, {
					"aria-label": t("admin.gameData.fields"),
					rows: g,
					rowKey: (e) => e.path,
					maxHeight: 360,
					onRowClick: (e) => {
						s(e.path), l(wo(e.value));
					},
					columns: [
						{
							key: "p",
							header: t("admin.gameData.path"),
							render: (e) => /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: e.path
							})
						},
						{
							key: "v",
							header: t("admin.gameData.value"),
							width: "150px",
							render: (e) => /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: wo(e.value)
							})
						},
						{
							key: "t",
							header: t("admin.gameData.type"),
							width: "150px",
							render: (e) => /* @__PURE__ */ T("span", {
								className: "k-hint",
								children: e.type.replace(/^keen::/, "")
							})
						}
					]
				})] }) : a.data === void 0 ? a.error ? /* @__PURE__ */ T("div", {
					className: "k-error-text",
					children: a.error.message
				}) : null : /* @__PURE__ */ T(qn, { value: a.data })
			]
		})
	});
}
var Do = Br(To), Oo = {
	everyone: "ok",
	admin: "warn",
	console: "err"
};
function ko() {
	let e = M(), t = Y(), n = c(), r = I("permissions_get", {}, { refetchInterval: 8e3 }), [i, a] = x("groups"), [o, s] = x(null), [l, u] = x({
		sid: "",
		node: ""
	}), d = (e) => n.setQueriesData({ queryKey: [
		"admin",
		t,
		"permissions_get"
	] }, e), f = r.data;
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.permissions.title"),
			sub: e("admin.permissions.sub")
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ E(Vr, {
			ops: [
				"permissions_get",
				"permissions_group",
				"permissions_player_node"
			],
			what: e("admin.permissions.title"),
			children: [
				/* @__PURE__ */ T(Un, {
					value: i,
					onChange: a,
					items: [
						{
							id: "groups",
							label: e("admin.permissions.tabGroups"),
							count: f ? Object.keys(f.groups).length : void 0
						},
						{
							id: "players",
							label: e("admin.pages.players")
						},
						{
							id: "check",
							label: e("admin.permissions.tabCheck")
						},
						{
							id: "nodes",
							label: e("admin.permissions.tabNodes"),
							count: f?.nodes?.length
						}
					]
				}),
				r.error ? /* @__PURE__ */ T(H, {
					tone: "warn",
					title: e("admin.permissions.readFailed"),
					children: r.error.message
				}) : null,
				f ? null : /* @__PURE__ */ T("div", {
					className: "k-muted",
					children: e("admin.map.loading")
				}),
				/* @__PURE__ */ E("datalist", {
					id: "ka-nodes",
					children: [
						(f?.nodes ?? []).map((e) => /* @__PURE__ */ T("option", {
							value: e.node,
							children: e.description
						}, e.node)),
						[...new Set((f?.nodes ?? []).map((e) => e.node.split(".")[0]))].map((t) => /* @__PURE__ */ T("option", {
							value: `${t}.*`,
							children: e("admin.permissions.everyNodeOf", { mod: t })
						}, t)),
						/* @__PURE__ */ T("option", {
							value: "*",
							children: e("admin.permissions.everyNode")
						})
					]
				}),
				f && i === "groups" ? /* @__PURE__ */ T(No, {
					d: f,
					put: d
				}) : null,
				f && i === "players" ? /* @__PURE__ */ T(Po, {
					d: f,
					put: d,
					person: o,
					setPerson: s,
					check: (e) => {
						u({
							sid: e,
							node: ""
						}), a("check");
					}
				}) : null,
				f && i === "check" ? /* @__PURE__ */ T(Lo, {
					d: f,
					initial: l
				}) : null,
				f && i === "nodes" ? /* @__PURE__ */ T(Ro, {
					d: f,
					check: (e) => {
						u({
							sid: l.sid,
							node: e
						}), a("check");
					}
				}) : null
			]
		})
	] });
}
function Ao(e, t) {
	return F(e, {
		onSuccess: (e) => t(e),
		onError: (e) => K(e)
	});
}
function jo({ rules: e, onRemove: t }) {
	let n = M();
	return e.length ? /* @__PURE__ */ T("span", {
		className: "ka-chips",
		children: e.map((e) => {
			let r = e.startsWith("-"), i = e.replace(/^-/, "");
			return /* @__PURE__ */ E("span", {
				className: "ka-chip " + (r ? "deny" : "allow"),
				title: n(r ? "admin.permissions.denied" : "admin.permissions.allowed"),
				children: [
					r ? "−" : "+",
					" ",
					/* @__PURE__ */ T("span", {
						className: "k-mono",
						children: i
					}),
					/* @__PURE__ */ T("button", {
						type: "button",
						title: n("admin.permissions.removeRule"),
						onClick: () => t(i),
						children: "×"
					})
				]
			}, e);
		})
	}) : /* @__PURE__ */ T("span", {
		className: "k-muted",
		children: n("admin.permissions.noRules")
	});
}
function Mo({ onAdd: e }) {
	let t = M(), [n, r] = x(""), [i, a] = x("1"), o = () => {
		let t = n.trim().toLowerCase(), a = oi(t);
		if (a) return K({ message: a });
		e(t, i === "1"), r("");
	};
	return /* @__PURE__ */ E("span", {
		className: "ka-inline",
		children: [
			/* @__PURE__ */ T(R, {
				"aria-label": t("admin.permissions.node"),
				list: "ka-nodes",
				placeholder: t("admin.permissions.nodePlaceholder"),
				value: n,
				onChange: (e) => r(e.target.value),
				onKeyDown: (e) => e.key === "Enter" && o(),
				style: { width: 280 },
				mono: !0
			}),
			/* @__PURE__ */ T(z, {
				"aria-label": t("admin.permissions.allowOrDeny"),
				value: i,
				onChange: a,
				options: [{
					value: "1",
					label: t("admin.permissions.allow")
				}, {
					value: "0",
					label: t("admin.permissions.deny")
				}]
			}),
			/* @__PURE__ */ T(L, {
				size: "s",
				onClick: o,
				children: t("admin.permissions.addRule")
			})
		]
	});
}
function No({ d: e, put: t }) {
	let n = M(), r = Ao("permissions_group", t), i = Ao("permissions_group_remove", t), a = Ao("permissions_map_user_group", t), o = Ao("permissions_reload", t), [s, c] = x(""), l = Object.keys(e.groups), u = (e, t) => r.mutate(e, { onSuccess: () => G(t) }), d = () => {
		let e = s.trim();
		if (!ni.test(e)) return K({ message: n("admin.permissions.groupRule") });
		u({ group: e }, n("admin.permissions.created", { group: e })), c("");
	}, f = {};
	for (let t of e.serverUserGroups ?? []) (f[t.rights] ??= []).push(t.name);
	let p = String(e.file ?? "").replace(/^.*[\\/]kiln[\\/]/, "kiln/").replace(/\\/g, "/");
	return /* @__PURE__ */ E("div", {
		className: "k-grid k-grid-2",
		style: { alignItems: "start" },
		children: [/* @__PURE__ */ T(U, {
			title: n("admin.permissions.kilnGroups"),
			actions: /* @__PURE__ */ E("span", {
				className: "ka-inline",
				children: [/* @__PURE__ */ T(R, {
					"aria-label": n("admin.permissions.newGroup"),
					placeholder: n("admin.permissions.newGroupPlaceholder"),
					maxLength: 40,
					value: s,
					onChange: (e) => c(e.target.value),
					onKeyDown: (e) => e.key === "Enter" && d(),
					style: { width: 170 }
				}), /* @__PURE__ */ T(L, {
					size: "s",
					onClick: d,
					children: n("admin.permissions.create")
				})]
			}),
			children: /* @__PURE__ */ T("div", {
				className: "ka-list",
				children: l.map((t) => {
					let r = e.groups[t], a = t.toLowerCase() === String(e.defaultGroup).toLowerCase(), o = l.filter((e) => e !== t && !(r.inherits ?? []).includes(e));
					return /* @__PURE__ */ E("div", {
						className: "k-stack",
						style: { gap: 8 },
						children: [
							/* @__PURE__ */ E("div", {
								className: "ka-row-item",
								children: [
									/* @__PURE__ */ T("b", {
										className: "ka-person-name",
										children: t
									}),
									a ? /* @__PURE__ */ T(V, {
										tone: "ember",
										title: n("admin.permissions.everyPlayerTip"),
										children: n("admin.permissions.everyPlayer")
									}) : null,
									r.admin ? /* @__PURE__ */ T(V, {
										tone: "warn",
										children: n("admin.permissions.countsAdmin")
									}) : null,
									/* @__PURE__ */ T("span", { className: "k-spacer" }),
									/* @__PURE__ */ T(ln, {
										checked: !!r.admin,
										label: n("admin.players.stateAdmin"),
										onChange: (e) => u({
											group: t,
											admin: e
										}, n(e ? "admin.permissions.groupAdminOn" : "admin.permissions.groupAdminOff", { group: t }))
									}),
									/* @__PURE__ */ T(L, {
										size: "s",
										variant: "ghost",
										disabled: a,
										title: a ? n("admin.permissions.defaultNoRemove") : void 0,
										onClick: async () => {
											await q({
												title: n("admin.permissions.removeGroupTitle", { group: t }),
												body: /* @__PURE__ */ T("p", { children: n("admin.permissions.removeGroupBody") }),
												ok: n("common.remove"),
												danger: !0
											}) && i.mutate({ group: t }, { onSuccess: () => G(n("admin.permissions.removedGroup", { group: t })) });
										},
										children: n("common.remove")
									})
								]
							}),
							/* @__PURE__ */ E("div", {
								className: "ka-inline",
								children: [
									/* @__PURE__ */ T("span", {
										className: "ka-label",
										children: n("admin.permissions.inherits")
									}),
									/* @__PURE__ */ T("span", {
										className: "ka-chips",
										children: (r.inherits ?? []).length ? (r.inherits ?? []).map((e) => /* @__PURE__ */ E("span", {
											className: "ka-chip",
											children: [e, /* @__PURE__ */ T("button", {
												type: "button",
												title: n("admin.permissions.stopInheriting"),
												onClick: () => u({
													group: t,
													inherits: (r.inherits ?? []).filter((t) => t !== e)
												}, n("admin.permissions.noLongerInherits", {
													group: t,
													other: e
												})),
												children: "×"
											})]
										}, e)) : /* @__PURE__ */ T("span", {
											className: "k-muted",
											children: n("admin.permissions.nothing")
										})
									}),
									o.length ? /* @__PURE__ */ T(z, {
										"aria-label": n("admin.permissions.inherit"),
										value: "",
										onChange: (e) => e && u({
											group: t,
											inherits: [...r.inherits ?? [], e]
										}, n("admin.permissions.nowInherits", {
											group: t,
											other: e
										})),
										options: [{
											value: "",
											label: n("admin.permissions.inheritAdd")
										}, ...o]
									}) : null
								]
							}),
							/* @__PURE__ */ E("div", {
								className: "ka-inline",
								style: { alignItems: "flex-start" },
								children: [/* @__PURE__ */ T("span", {
									className: "ka-label",
									children: n("admin.permissions.rules")
								}), /* @__PURE__ */ T(jo, {
									rules: r.nodes ?? [],
									onRemove: (e) => u({
										group: t,
										node: e,
										allow: null
									}, n("admin.permissions.removedRule", {
										who: t,
										node: e
									}))
								})]
							}),
							/* @__PURE__ */ E("div", {
								className: "ka-inline",
								children: [/* @__PURE__ */ T("span", { className: "ka-label" }), /* @__PURE__ */ T(Mo, { onAdd: (e, r) => u({
									group: t,
									node: e,
									allow: r
								}, n(r ? "admin.permissions.groupAllows" : "admin.permissions.groupDenies", {
									group: t,
									node: e
								})) })]
							})
						]
					}, t);
				})
			})
		}), /* @__PURE__ */ E("div", {
			className: "k-stack",
			style: { gap: 16 },
			children: [
				/* @__PURE__ */ T(U, {
					title: n("admin.permissions.serverUserGroups"),
					sub: n("admin.permissions.serverUserGroupsSub"),
					flush: !0,
					children: /* @__PURE__ */ T(Gn, {
						"aria-label": n("admin.permissions.userGroups"),
						rows: e.serverUserGroups ?? [],
						rowKey: (e) => e.name,
						rowHeight: 72,
						columns: [{
							key: "u",
							header: n("admin.permissions.userGroup"),
							width: "minmax(0, 1fr)",
							render: (e) => /* @__PURE__ */ E("div", { children: [
								/* @__PURE__ */ T("b", { children: e.name }),
								/* @__PURE__ */ T("div", {
									className: "k-hint",
									children: e.rights === "None" ? n("admin.permissions.noRights") : e.rights
								}),
								f[e.rights].length > 1 ? /* @__PURE__ */ T("div", {
									className: "k-hint",
									style: { color: "var(--k-warn)" },
									children: n("admin.permissions.sameRights", { list: f[e.rights].filter((t) => t !== e.name).join(", ") })
								}) : null
							] })
						}, {
							key: "k",
							header: n("admin.permissions.kilnGroups"),
							width: "minmax(0, 1.2fr)",
							render: (t) => {
								let r = e.userGroups?.[t.name] ?? [];
								return /* @__PURE__ */ T("div", {
									className: "ka-flags",
									children: l.map((e) => /* @__PURE__ */ E("label", { children: [
										/* @__PURE__ */ T("input", {
											type: "checkbox",
											checked: r.includes(e),
											onChange: (i) => {
												let o = i.target.checked ? [...r, e] : r.filter((t) => t !== e);
												a.mutate({
													userGroup: t.name,
													groups: o
												}, { onSuccess: () => G(n("admin.permissions.userGroupMapped", {
													name: t.name,
													groups: o.join(", ") || n("admin.permissions.noKilnGroup")
												})) });
											}
										}),
										" ",
										e
									] }, e))
								});
							}
						}],
						empty: /* @__PURE__ */ T(W, {
							title: n("admin.permissions.noUserGroups"),
							hint: n("admin.permissions.noUserGroupsHint")
						})
					})
				}),
				/* @__PURE__ */ E(H, {
					tone: "info",
					children: [
						j("admin.permissions.howItWorks", {
							group: /* @__PURE__ */ T("b", { children: e.defaultGroup }),
							prefix: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: "prefix.*"
							}),
							star: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: "*"
							})
						}),
						" ",
						e.kickBanIsAdmin ? n("admin.permissions.kickBanAdmin") : ""
					]
				}),
				/* @__PURE__ */ T(U, {
					title: n("admin.permissions.theFile"),
					sub: /* @__PURE__ */ T("span", {
						className: "k-mono",
						children: p
					}),
					actions: /* @__PURE__ */ T(L, {
						size: "s",
						variant: "ghost",
						loading: o.isPending,
						onClick: () => o.mutate({}, { onSuccess: () => G(n("admin.permissions.reread")) }),
						children: n("admin.permissions.rereadNow")
					}),
					children: /* @__PURE__ */ T("span", {
						className: "k-hint",
						children: n("admin.permissions.fileHint")
					})
				})
			]
		})]
	});
}
function Po({ d: e, put: t, person: n, setPerson: r, check: i }) {
	let a = M(), o = Jt(Y()), s = Ft(), c = new Map((s.state?.players ?? []).filter((e) => e.steamId).map((e) => [e.steamId, e])), l = nn(o, e.players ?? {});
	for (let [e, t] of c) l.some((t) => t.steamId === e) || l.unshift({
		steamId: e,
		name: t.name,
		firstSeen: 0,
		lastSeen: Date.now() / 1e3
	});
	let [u, d] = x(""), f = n ?? l[0]?.steamId ?? null;
	return /* @__PURE__ */ E("div", {
		className: "ka-split list-left",
		children: [/* @__PURE__ */ E(U, {
			title: a("admin.pages.players"),
			flush: !0,
			children: [/* @__PURE__ */ E("div", {
				style: {
					padding: "10px 12px",
					borderBottom: "1px solid var(--k-line)"
				},
				className: "ka-inline",
				children: [/* @__PURE__ */ T(R, {
					"aria-label": "SteamID64",
					placeholder: "SteamID64",
					maxLength: 17,
					value: u,
					onChange: (e) => d(e.target.value),
					style: { width: 170 },
					mono: !0
				}), /* @__PURE__ */ T(L, {
					size: "s",
					onClick: () => {
						let e = ai(u);
						if (e) return K({ message: e });
						r(u.trim()), d("");
					},
					children: a("common.open")
				})]
			}), /* @__PURE__ */ T("div", {
				className: "ka-pick",
				style: { padding: 6 },
				children: l.length ? l.map((e) => /* @__PURE__ */ E("button", {
					className: e.steamId === f ? "active" : "",
					onClick: () => r(e.steamId),
					children: [
						c.has(e.steamId) ? /* @__PURE__ */ T(V, {
							tone: "ok",
							children: a("admin.permissions.on")
						}) : null,
						/* @__PURE__ */ T("span", { children: e.name || a("admin.players.unknown") }),
						/* @__PURE__ */ T("small", {
							className: "k-mono",
							children: e.steamId
						})
					]
				}, e.steamId)) : /* @__PURE__ */ T(W, {
					title: a("admin.permissions.noPlayers"),
					hint: a("admin.permissions.noPlayersHint")
				})
			})]
		}), f ? /* @__PURE__ */ T(Fo, {
			d: e,
			put: t,
			sid: f,
			name: l.find((e) => e.steamId === f)?.name ?? c.get(f)?.name ?? null,
			online: c.get(f) ?? null,
			lastSeen: l.find((e) => e.steamId === f)?.lastSeen ?? 0,
			check: i
		}) : /* @__PURE__ */ T(U, { children: /* @__PURE__ */ T(W, { title: a("admin.common.pickPlayer") }) })]
	});
}
function Fo({ d: e, put: t, sid: n, name: r, online: i, lastSeen: a, check: o }) {
	let s = M(), c = Ao("permissions_player_node", t), l = Ao("permissions_player_group", t), u = e.players?.[n] ?? {
		groups: [],
		nodes: [],
		name: null
	}, d = Object.keys(e.groups).filter((e) => !u.groups.includes(e)), f = r || u.name || n, p = r ? { name: r.slice(0, 64) } : {}, m = i ? (e.serverUserGroups ?? []).filter((e) => e.rights === i.permissions).map((e) => e.name) : [], h = m.length === 1 ? e.userGroups?.[m[0]] ?? [] : [];
	return /* @__PURE__ */ T(U, {
		title: /* @__PURE__ */ E("span", {
			className: "ka-person",
			children: [/* @__PURE__ */ T(Mr, {
				name: f,
				online: !!i
			}), /* @__PURE__ */ T("span", { children: f })]
		}),
		sub: /* @__PURE__ */ T("span", {
			className: "k-mono",
			children: u.name && u.name !== r ? s("admin.permissions.sidNote", {
				sid: n,
				note: u.name
			}) : n
		}),
		actions: /* @__PURE__ */ T(L, {
			size: "s",
			onClick: () => o(n),
			children: s("admin.permissions.checkNode")
		}),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			style: { gap: 14 },
			children: [
				i ? /* @__PURE__ */ T(Hn, { rows: [[s("admin.combat.inWorld"), /* @__PURE__ */ E(w, { children: [
					i.admin ? /* @__PURE__ */ T(V, {
						tone: "warn",
						children: s("admin.permissions.adminBadge")
					}) : null,
					" ",
					i.permissions
				] })], [s("admin.permissions.userGroup"), m.length === 1 ? /* @__PURE__ */ T(w, { children: h.length ? j("admin.permissions.ugVia", {
					group: /* @__PURE__ */ T("b", { children: m[0] }),
					via: h.join(", ")
				}) : j("admin.permissions.ugNone", { group: /* @__PURE__ */ T("b", { children: m[0] }) }) }) : m.length ? s("admin.permissions.ambiguous", { list: m.join(s("admin.permissions.orSep")) }) : s("admin.permissions.noneMatches")]] }) : a ? /* @__PURE__ */ T("span", {
					className: "k-hint",
					children: s("admin.permissions.lastSeen", { ago: Fr(a) })
				}) : null,
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						/* @__PURE__ */ T("span", {
							className: "ka-label",
							children: s("admin.permissions.tabGroups")
						}),
						/* @__PURE__ */ T("span", {
							className: "ka-chips",
							children: u.groups.length ? u.groups.map((e) => /* @__PURE__ */ E("span", {
								className: "ka-chip",
								children: [e, /* @__PURE__ */ T("button", {
									type: "button",
									title: s("admin.permissions.takeOut"),
									onClick: () => l.mutate({
										steamId: n,
										group: e,
										add: !1
									}, { onSuccess: () => G(s("admin.permissions.left", {
										who: f,
										group: e
									})) }),
									children: "×"
								})]
							}, e)) : /* @__PURE__ */ T("span", {
								className: "k-muted",
								children: s("admin.permissions.onlyEveryone")
							})
						}),
						d.length ? /* @__PURE__ */ T(z, {
							"aria-label": s("admin.permissions.addToGroup"),
							value: "",
							onChange: (e) => e && l.mutate({
								steamId: n,
								group: e,
								add: !0,
								...p
							}, { onSuccess: () => G(s("admin.permissions.joined", {
								who: f,
								group: e
							})) }),
							options: [{
								value: "",
								label: s("admin.permissions.addToGroupOption")
							}, ...d]
						}) : null
					]
				}),
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					style: { alignItems: "flex-start" },
					children: [/* @__PURE__ */ T("span", {
						className: "ka-label",
						children: s("admin.permissions.ownRules")
					}), /* @__PURE__ */ T(jo, {
						rules: u.nodes,
						onRemove: (e) => c.mutate({
							steamId: n,
							node: e,
							allow: null
						}, { onSuccess: () => G(s("admin.permissions.removedRule", {
							who: f,
							node: e
						})) })
					})]
				}),
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [/* @__PURE__ */ T("span", { className: "ka-label" }), /* @__PURE__ */ T(Mo, { onAdd: (e, t) => c.mutate({
						steamId: n,
						node: e,
						allow: t,
						...p
					}, { onSuccess: () => G(s(t ? "admin.permissions.playerAllowed" : "admin.permissions.playerDenied", {
						who: f,
						node: e
					})) }) })]
				}),
				/* @__PURE__ */ T("span", {
					className: "k-hint",
					children: s("admin.permissions.ownBeats")
				})
			]
		})
	});
}
function Io(e) {
	if (!e) return "";
	let [t, n] = e.split(":");
	return t === "default" ? A("admin.permissions.fromDefault", { level: String(n) }) : t === "player" ? A("admin.permissions.fromPlayer") : t === "group" ? A("admin.permissions.fromGroup", { group: String(n) }) : e;
}
function Lo({ d: e, initial: t }) {
	let n = M(), r = nn(Jt(Y()), e.players ?? {}), [i, a] = x(t.sid), [o, s] = x(t.node), [c, l] = x(null), u = F("permissions_check"), d = () => {
		if (ai(i)) return K({ message: n("admin.permissions.pickOrSid") });
		let e = o.trim().toLowerCase(), t = oi(e);
		if (t) return K({ message: t });
		u.mutate({
			steamId: i.trim(),
			node: e
		}, {
			onSuccess: (e) => l({
				...e,
				who: r.find((e) => e.steamId === i.trim())?.name || i
			}),
			onError: (e) => {
				l(null), K(e);
			}
		});
	};
	return /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(U, {
		title: n("admin.permissions.checkTitle"),
		sub: n("admin.permissions.checkSub"),
		children: /* @__PURE__ */ E("div", {
			className: "ka-inline",
			children: [
				/* @__PURE__ */ T(z, {
					"aria-label": n("admin.zones.player"),
					value: r.some((e) => e.steamId === i) ? i : "",
					onChange: (e) => e && a(e),
					options: [{
						value: "",
						label: n("admin.permissions.playerOption")
					}, ...r.map((e) => ({
						value: e.steamId,
						label: e.name || e.steamId
					}))]
				}),
				/* @__PURE__ */ T("span", {
					className: "k-muted",
					children: n("admin.permissions.or")
				}),
				/* @__PURE__ */ T(R, {
					"aria-label": "SteamID64",
					placeholder: "SteamID64",
					maxLength: 17,
					value: i,
					onChange: (e) => a(e.target.value),
					style: { width: 170 },
					mono: !0
				}),
				/* @__PURE__ */ T(R, {
					"aria-label": n("admin.permissions.node"),
					list: "ka-nodes",
					placeholder: n("admin.permissions.nodeCheckPlaceholder"),
					value: o,
					onChange: (e) => s(e.target.value),
					onKeyDown: (e) => e.key === "Enter" && d(),
					style: { width: 300 },
					mono: !0
				}),
				/* @__PURE__ */ T(L, {
					variant: "primary",
					loading: u.isPending,
					onClick: d,
					children: n("admin.permissions.tabCheck")
				})
			]
		})
	}), c ? /* @__PURE__ */ T(U, { children: /* @__PURE__ */ E("div", {
		className: "k-stack",
		children: [/* @__PURE__ */ E("div", {
			className: "ka-inline",
			children: [
				/* @__PURE__ */ T(V, {
					tone: c.allowed ? "ok" : "err",
					children: c.allowed ? n("admin.permissions.allowedCap") : n("admin.permissions.deniedCap")
				}),
				/* @__PURE__ */ T("span", {
					className: "k-mono",
					children: c.node
				}),
				/* @__PURE__ */ T("span", {
					className: "k-muted",
					children: n("admin.permissions.forWho", { who: c.who })
				})
			]
		}), /* @__PURE__ */ T(Hn, { rows: [
			[n("admin.permissions.decidedBy"), /* @__PURE__ */ E(w, { children: [
				c.rule ? j("admin.permissions.ruleX", { rule: /* @__PURE__ */ T("span", {
					className: "k-mono",
					children: c.rule
				}) }) : n("admin.permissions.noRuleMatched"),
				" ",
				c.from ? /* @__PURE__ */ E("span", {
					className: "k-muted",
					children: [
						"(",
						Io(c.from),
						")"
					]
				}) : null
			] })],
			[n("admin.permissions.nodeLevel"), /* @__PURE__ */ T(V, {
				tone: Oo[String(c.level).toLowerCase()] ?? "idle",
				children: String(c.level).toLowerCase()
			})],
			[n("admin.permissions.tabGroups"), c.groups.length ? c.groups.join(", ") : "-"]
		] })]
	}) }) : null] });
}
function Ro({ d: e, check: t }) {
	let n = M();
	return /* @__PURE__ */ T(U, {
		title: n("admin.permissions.registered"),
		sub: n("admin.permissions.registeredSub"),
		flush: !0,
		children: /* @__PURE__ */ T(Gn, {
			"aria-label": n("admin.permissions.tabNodes"),
			rows: e.nodes ?? [],
			rowKey: (e) => e.node,
			columns: [
				{
					key: "n",
					header: n("admin.permissions.node"),
					width: "minmax(0, 1.4fr)",
					render: (e) => /* @__PURE__ */ T("span", {
						className: "k-mono",
						children: e.node
					}),
					sort: (e, t) => e.node.localeCompare(t.node)
				},
				{
					key: "l",
					header: n("admin.permissions.default"),
					width: "110px",
					render: (e) => /* @__PURE__ */ T(V, {
						tone: Oo[e.level] ?? "idle",
						children: e.level
					})
				},
				{
					key: "d",
					header: n("admin.permissions.what"),
					render: (e) => e.description ?? ""
				},
				{
					key: "o",
					header: n("admin.content.mod"),
					width: "120px",
					render: (e) => /* @__PURE__ */ T("span", {
						className: "k-mono k-muted",
						children: e.owner ?? ""
					})
				},
				{
					key: "c",
					header: "",
					width: "90px",
					align: "right",
					render: (e) => /* @__PURE__ */ T(L, {
						size: "s",
						variant: "ghost",
						onClick: () => t(e.node),
						children: n("admin.permissions.checkMore")
					})
				}
			],
			empty: /* @__PURE__ */ T(W, { title: n("admin.permissions.noNodes") })
		})
	});
}
var zo = Br(ko), Bo = () => [
	{
		id: "items",
		label: A("admin.content.kindItems")
	},
	{
		id: "recipes",
		label: A("admin.content.kindRecipes")
	},
	{
		id: "loot",
		label: A("admin.content.kindLoot")
	},
	{
		id: "drops",
		label: A("admin.content.kindDrops")
	}
];
function Vo() {
	let e = M(), t = I("content_status", {}, { refetchInterval: 15e3 }), [n, r] = x("lookup"), i = t.data;
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.content.title"),
			sub: e("admin.content.sub")
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ E(Vr, {
			ops: "content_status",
			what: e("admin.content.what"),
			children: [
				/* @__PURE__ */ T(H, {
					tone: "ember",
					title: e("admin.content.tempTitle"),
					children: j("admin.content.tempBody", { path: /* @__PURE__ */ T("span", {
						className: "k-mono",
						children: "content/*.json"
					}) })
				}),
				i ? /* @__PURE__ */ E(Ar, { children: [
					/* @__PURE__ */ T(kr, {
						k: e("admin.content.statRegistries"),
						v: i.ready ? e("admin.content.ready") : e("admin.content.loadingState"),
						s: i.detail
					}),
					/* @__PURE__ */ T(kr, {
						k: e("admin.content.kindItems"),
						v: i.items,
						s: e("admin.content.statLoot", { n: i.loot })
					}),
					/* @__PURE__ */ T(kr, {
						k: e("admin.content.kindRecipes"),
						v: i.recipes,
						s: e("admin.content.statDrops", { n: i.drops })
					}),
					/* @__PURE__ */ T(kr, {
						k: e("admin.content.statChanged"),
						v: i.changed,
						s: e("admin.content.statChangedSub")
					}),
					/* @__PURE__ */ T(kr, {
						k: e("admin.content.dataPacks"),
						v: i.packs,
						s: i.stackGuard ? e("admin.content.statPacksGuard", {
							errors: i.errors,
							conflicts: i.conflicts
						}) : e("admin.content.statPacks", {
							errors: i.errors,
							conflicts: i.conflicts
						})
					})
				] }) : t.error ? /* @__PURE__ */ T(H, {
					tone: "warn",
					children: t.error.message
				}) : null,
				/* @__PURE__ */ T(Un, {
					value: n,
					onChange: r,
					items: [
						{
							id: "lookup",
							label: e("admin.content.lookUp")
						},
						{
							id: "packs",
							label: e("admin.content.dataPacks"),
							count: i?.packs
						},
						{
							id: "definitions",
							label: e("admin.content.newContent")
						}
					]
				}),
				T(n === "lookup" ? Ho : n === "packs" ? Ko : qo, {})
			]
		})
	] });
}
function Ho() {
	let e = M(), [t, n] = x("items"), [r, i] = x(""), [a, o] = x(null), s = zr(r.trim(), 200), c = P("content_entry"), l = I("content_search", {
		kind: t,
		q: s,
		limit: 30
	}, {
		enabled: s.length >= 2 && s !== a,
		staleTime: 3e4
	}), u = y(() => (l.data ?? []).map((t) => ({
		key: t.id,
		label: t.name,
		sub: /* @__PURE__ */ E(w, { children: [
			t.modified ? /* @__PURE__ */ T(V, {
				tone: "ember",
				children: e("admin.gameData.changed")
			}) : null,
			" ",
			/* @__PURE__ */ T("span", {
				className: "k-mono",
				children: t.id
			})
		] }),
		value: t.name
	})), [l.data, e]);
	return /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ E(U, {
		title: e("admin.content.lookUp"),
		actions: /* @__PURE__ */ T(Wn, {
			value: t,
			onChange: (e) => {
				n(e), o(null), i("");
			},
			options: Bo(),
			"aria-label": e("admin.content.registry")
		}),
		children: [/* @__PURE__ */ T(Nr, {
			value: r,
			onChange: i,
			suggestions: s === a ? [] : u,
			placeholder: e("admin.content.searchPlaceholder"),
			mono: !0,
			onPick: (e) => {
				i(e.value), o(e.value);
			},
			onEnter: () => r.trim() && o(r.trim())
		}), /* @__PURE__ */ E("div", {
			className: "k-hint",
			style: { marginTop: 6 },
			children: [
				e("admin.content.debugHint"),
				" ",
				l.error ? /* @__PURE__ */ T("span", {
					className: "k-error-text",
					children: l.error.message
				}) : null
			]
		})]
	}), a ? c ? /* @__PURE__ */ T(Uo, {
		kind: t,
		name: a
	}) : /* @__PURE__ */ T(Go, {
		kind: t,
		entry: a
	}) : /* @__PURE__ */ T(U, { children: /* @__PURE__ */ T(W, {
		title: e("admin.content.lookEmpty"),
		hint: e("admin.content.lookEmptyHint")
	}) })] });
}
function Uo({ kind: e, name: t }) {
	let n = M(), r = Y(), i = c(), a = I("content_entry", {
		kind: e,
		entry: t
	}, { staleTime: 5e3 }), [o, s] = x(""), [l, u] = x(!0), [d, f] = x(!1), [p, m] = x(null), [h, g] = x({}), _ = F("content_set"), v = F("content_reset"), y = (n, a) => i.setQueriesData({ queryKey: [
		"admin",
		r,
		"content_entry",
		{
			kind: e,
			entry: t
		}
	] }, (e) => e && {
		...e,
		fields: e.fields.map((e) => e.path === n ? {
			...e,
			value: a.value,
			original: a.original ?? e.original,
			changed: a.value !== (a.original ?? e.original)
		} : e)
	});
	if (a.error) return /* @__PURE__ */ T(H, {
		tone: "warn",
		children: a.error.message
	});
	let b = a.data;
	if (!b) return /* @__PURE__ */ T("div", {
		className: "k-muted",
		children: n("admin.content.readingName", { name: t })
	});
	let S = o.toLowerCase(), C = b.fields.filter((e) => (!l || e.writable) && (!d || e.changed) && (!S || e.path.toLowerCase().includes(S))).sort((e, t) => Number(!!t.changed) - Number(!!e.changed)), ee = (t, r) => {
		_.mutate({
			kind: e,
			entry: b.name,
			field: t.path,
			value: r
		}, {
			onSuccess: (e) => {
				y(t.path, e), m(null), g(({ [t.path]: e, ...n }) => n), G(`${b.name}.${t.path} = ${e.value}`, n("admin.content.untilReload"));
			},
			onError: (e) => g((n) => ({
				...n,
				[t.path]: e.message
			}))
		});
	}, D = (e, t) => t?.length ? /* @__PURE__ */ E("div", {
		className: "ka-inline",
		children: [/* @__PURE__ */ T("b", {
			style: { fontSize: 12.5 },
			children: e
		}), /* @__PURE__ */ T("span", {
			className: "ka-chips",
			children: t.map((e, t) => /* @__PURE__ */ E("span", {
				className: "ka-chip plain",
				children: [
					e.count,
					" × ",
					e.name ?? e.item
				]
			}, t))
		})]
	}) : null;
	return /* @__PURE__ */ T(U, {
		title: b.name,
		sub: /* @__PURE__ */ E(w, { children: [
			/* @__PURE__ */ T("span", {
				className: "k-mono",
				children: b.id
			}),
			" · ",
			b.kind,
			" · ",
			/* @__PURE__ */ T("span", {
				className: "k-mono",
				children: b.guid
			})
		] }),
		actions: b.modified ? /* @__PURE__ */ T(V, {
			tone: "ember",
			title: n("admin.content.changedTip"),
			children: n("admin.gameData.changed")
		}) : null,
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			style: { gap: 12 },
			children: [
				D(n("admin.content.needs"), b.inputs),
				D(n("admin.content.makes"), b.outputs),
				b.stacks?.length ? /* @__PURE__ */ E("table", {
					className: "ka-fields",
					children: [/* @__PURE__ */ T("thead", { children: /* @__PURE__ */ E("tr", { children: [
						/* @__PURE__ */ T("th", { children: "#" }),
						/* @__PURE__ */ T("th", { children: n("admin.items.colItem") }),
						/* @__PURE__ */ T("th", {
							className: "num",
							children: n("admin.creatures.count")
						}),
						/* @__PURE__ */ T("th", {
							className: "num",
							children: n("admin.content.chance")
						}),
						/* @__PURE__ */ T("th", {
							className: "num",
							children: n("admin.content.weight")
						})
					] }) }), /* @__PURE__ */ T("tbody", { children: b.stacks.map((e) => /* @__PURE__ */ E("tr", { children: [
						/* @__PURE__ */ T("td", { children: e.index }),
						/* @__PURE__ */ T("td", { children: e.name ?? e.item }),
						/* @__PURE__ */ E("td", {
							className: "num",
							children: [
								e.countMin,
								"–",
								e.countMax
							]
						}),
						/* @__PURE__ */ T("td", {
							className: "num",
							children: e.chance
						}),
						/* @__PURE__ */ T("td", {
							className: "num",
							children: e.weight
						})
					] }, e.index)) })]
				}) : null,
				/* @__PURE__ */ E("div", {
					className: "ka-inline",
					children: [
						/* @__PURE__ */ T(R, {
							"aria-label": n("admin.gameData.filterFields"),
							placeholder: n("admin.content.filterFieldsPlaceholder"),
							value: o,
							onChange: (e) => s(e.target.value),
							style: { width: 220 }
						}),
						/* @__PURE__ */ T(ln, {
							checked: l,
							onChange: u,
							label: n("admin.content.changeableOnly")
						}),
						/* @__PURE__ */ T(ln, {
							checked: d,
							onChange: f,
							label: n("admin.content.changedOnly")
						}),
						/* @__PURE__ */ T("span", { className: "k-spacer" }),
						/* @__PURE__ */ T("span", {
							className: "k-muted",
							children: n("admin.content.fieldsCount", {
								n: C.length,
								total: b.fields.length
							})
						})
					]
				}),
				C.length ? /* @__PURE__ */ E("table", {
					className: "ka-fields",
					children: [/* @__PURE__ */ T("thead", { children: /* @__PURE__ */ E("tr", { children: [
						/* @__PURE__ */ T("th", { children: n("admin.creatures.field") }),
						/* @__PURE__ */ T("th", { children: n("admin.content.now") }),
						/* @__PURE__ */ T("th", { children: n("admin.content.gameValue") }),
						/* @__PURE__ */ T("th", {})
					] }) }), /* @__PURE__ */ T("tbody", { children: C.slice(0, 200).map((t) => /* @__PURE__ */ E("tr", {
						className: (t.changed ? "changed " : "") + (t.writable ? "" : "ro"),
						children: [
							/* @__PURE__ */ E("td", {
								className: "path",
								children: [t.path, /* @__PURE__ */ T("div", {
									className: "k-hint",
									children: t.writable ? t.type : n("admin.content.typeReadOnly", { type: t.type })
								})]
							}),
							p === t.path ? /* @__PURE__ */ E("td", {
								colSpan: 2,
								children: [/* @__PURE__ */ T(Wo, {
									f: t,
									busy: _.isPending,
									onCancel: () => m(null),
									onSet: (e) => ee(t, e)
								}), h[t.path] ? /* @__PURE__ */ T("div", {
									className: "k-error-text",
									children: h[t.path]
								}) : null]
							}) : /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T("td", {
								className: "val",
								children: t.error ? /* @__PURE__ */ T("span", {
									className: "k-muted",
									children: t.error
								}) : t.value === "" ? n("admin.content.none") : t.value
							}), /* @__PURE__ */ T("td", {
								className: "val k-muted",
								children: t.changed ? t.original === "" ? n("admin.content.none") : t.original : ""
							})] }),
							/* @__PURE__ */ E("td", {
								style: {
									whiteSpace: "nowrap",
									textAlign: "right"
								},
								children: [
									t.writable && !t.error && p !== t.path ? /* @__PURE__ */ T(L, {
										size: "s",
										onClick: () => m(t.path),
										children: n("admin.zones.change")
									}) : null,
									" ",
									t.changed && t.writable ? /* @__PURE__ */ T(L, {
										size: "s",
										variant: "ghost",
										title: n("admin.content.resetTip"),
										loading: v.isPending && v.variables?.field === t.path,
										onClick: () => v.mutate({
											kind: e,
											entry: b.name,
											field: t.path
										}, {
											onSuccess: (e) => {
												y(t.path, e), G(n("admin.content.backTo", {
													name: b.name,
													path: t.path,
													value: e.value
												}));
											},
											onError: (e) => K(e)
										}),
										children: n("common.reset")
									}) : null
								]
							})
						]
					}, t.path)) })]
				}) : /* @__PURE__ */ T(W, { title: n("admin.content.noFieldMatches") }),
				C.length > 200 ? /* @__PURE__ */ T("div", {
					className: "k-hint",
					children: n("admin.content.first200", { n: C.length })
				}) : null
			]
		})
	});
}
function Wo({ f: e, busy: t, onSet: n, onCancel: r }) {
	let i = M(), a = [
		"int",
		"float",
		"time"
	].includes(e.valueType) && e.enumKind === "plain", [o, s] = x(a ? String(e.value ?? "").replace(/s$/, "") : String(e.value ?? "")), [c, l] = x(new Set(String(e.value ?? "").split(",").map((e) => e.trim()).filter(Boolean))), u = () => {
		if (e.enumKind === "flags") return [...c].join(",");
		if (e.enumKind === "enum") return o;
		if (e.valueType === "bool") return o === "true";
		if (a) {
			let t = Number(o);
			return o.trim() === "" || !Number.isFinite(t) ? Error(i("admin.content.needNumber")) : e.valueType === "int" && !Number.isInteger(t) ? Error(i("admin.content.needWhole")) : t;
		}
		return o.length > 2e3 ? Error(i("admin.content.textMax")) : o;
	}, d = () => {
		let e = u();
		if (e instanceof Error) return K({ message: e.message });
		n(e);
	}, f;
	return f = e.enumKind === "flags" ? /* @__PURE__ */ T("div", {
		className: "ka-flags",
		children: (e.options ?? []).map((e) => /* @__PURE__ */ E("label", { children: [
			/* @__PURE__ */ T("input", {
				type: "checkbox",
				className: "k-check",
				checked: c.has(e),
				onChange: (t) => {
					let n = new Set(c);
					t.target.checked ? n.add(e) : n.delete(e), l(n);
				}
			}),
			" ",
			e
		] }, e))
	}) : e.enumKind === "enum" ? /* @__PURE__ */ T(z, {
		"aria-label": e.path,
		value: o,
		onChange: s,
		options: (e.options ?? []).includes(o) ? e.options : [o, ...e.options ?? []]
	}) : e.valueType === "bool" ? /* @__PURE__ */ T(z, {
		"aria-label": e.path,
		value: o === "true" ? "true" : "false",
		onChange: s,
		options: ["true", "false"]
	}) : /* @__PURE__ */ T(R, {
		"aria-label": e.path,
		autoFocus: !0,
		inputMode: a ? "decimal" : void 0,
		value: o,
		onChange: (e) => s(e.target.value),
		onKeyDown: (e) => {
			e.key === "Enter" && d(), e.key === "Escape" && r();
		},
		style: { width: 180 },
		mono: !0
	}), /* @__PURE__ */ E("div", {
		className: "ka-inline",
		children: [
			f,
			e.valueType === "time" ? /* @__PURE__ */ T("span", {
				className: "k-hint",
				children: i("admin.content.seconds")
			}) : null,
			/* @__PURE__ */ T(L, {
				size: "s",
				loading: t,
				onClick: d,
				children: i("admin.gameData.set")
			}),
			/* @__PURE__ */ T(L, {
				size: "s",
				variant: "ghost",
				onClick: r,
				children: i("common.cancel")
			})
		]
	});
}
function Go({ kind: e, entry: t }) {
	let n = M(), [r, i] = x(""), [a, o] = x(null), [s, c] = x(""), l = F("content_get", {
		onSuccess: o,
		onError: (e) => K(e)
	}), u = F("content_set", {
		onSuccess: (e) => {
			o(e), G(n("admin.content.setTemporary", {
				entry: t,
				field: r,
				value: e.value
			}));
		},
		onError: (e) => K(e)
	}), d = F("content_reset", {
		onSuccess: o,
		onError: (e) => K(e)
	}), f = () => ii.test(r.trim()) ? !0 : (K({ message: n("admin.content.fieldRule") }), !1), p = () => s === "true" ? !0 : s === "false" ? !1 : s.trim() !== "" && Number.isFinite(Number(s)) ? Number(s) : s;
	return /* @__PURE__ */ T(U, {
		title: t,
		sub: n("admin.content.singleSub"),
		children: /* @__PURE__ */ E("div", {
			className: "k-stack",
			children: [/* @__PURE__ */ E("div", {
				className: "ka-inline",
				children: [/* @__PURE__ */ T(R, {
					"aria-label": n("admin.creatures.field"),
					placeholder: n("admin.content.fieldPlaceholder"),
					value: r,
					onChange: (e) => i(e.target.value),
					mono: !0,
					style: { width: 260 }
				}), /* @__PURE__ */ T(L, {
					onClick: () => f() && l.mutate({
						kind: e,
						entry: t,
						field: r.trim()
					}),
					children: n("admin.content.read")
				})]
			}), a ? /* @__PURE__ */ E("div", {
				className: "ka-inline",
				children: [
					/* @__PURE__ */ T("span", { children: j("admin.content.nowGame", {
						value: /* @__PURE__ */ T("b", {
							className: "k-mono",
							children: a.value
						}),
						original: /* @__PURE__ */ T("span", {
							className: "k-mono",
							children: a.original
						})
					}) }),
					/* @__PURE__ */ T(R, {
						"aria-label": n("admin.content.newValue"),
						placeholder: n("admin.content.newValuePlaceholder"),
						value: s,
						onChange: (e) => c(e.target.value),
						style: { width: 160 }
					}),
					/* @__PURE__ */ T(L, {
						size: "s",
						onClick: () => f() && u.mutate({
							kind: e,
							entry: t,
							field: r.trim(),
							value: p()
						}),
						children: n("admin.gameData.set")
					}),
					/* @__PURE__ */ T(L, {
						size: "s",
						variant: "ghost",
						onClick: () => f() && d.mutate({
							kind: e,
							entry: t,
							field: r.trim()
						}),
						children: n("common.reset")
					})
				]
			}) : null]
		})
	});
}
function Ko() {
	let e = M(), t = Y(), n = c(), r = I("content_report", {}, {
		enabled: P("content_report"),
		refetchInterval: 3e4
	}), i = F("content_reload", {
		onSuccess: (r) => {
			n.setQueriesData({ queryKey: [
				"admin",
				t,
				"content_report"
			] }, r), G(e("admin.content.packsApplied"));
		},
		onError: (e) => K(e),
		invalidates: ["content_status"]
	}), a = y(() => {
		try {
			return JSON.parse(r.data?.report ?? "{}");
		} catch {
			return {};
		}
	}, [r.data]);
	return /* @__PURE__ */ E("div", {
		className: "k-grid k-grid-2",
		style: { alignItems: "start" },
		children: [/* @__PURE__ */ T(U, {
			title: e("admin.content.dataPacks"),
			sub: e("admin.content.packsSub"),
			actions: /* @__PURE__ */ T(L, {
				size: "s",
				loading: i.isPending,
				onClick: () => i.mutate({}),
				title: e("admin.content.reapplyTip"),
				children: e("admin.content.reapply")
			}),
			children: /* @__PURE__ */ E("div", {
				className: "ka-list",
				children: [(a.packs ?? []).length ? a.packs.map((t) => /* @__PURE__ */ E("div", {
					className: "k-stack",
					style: { gap: 4 },
					children: [
						/* @__PURE__ */ E("div", {
							className: "ka-row-item",
							children: [
								/* @__PURE__ */ T("b", {
									className: "k-mono",
									children: t.file
								}),
								/* @__PURE__ */ T("span", { className: "k-spacer" }),
								/* @__PURE__ */ T(V, {
									tone: "ok",
									children: e("admin.content.applied", { n: t.applied })
								}),
								t.errors?.length ? /* @__PURE__ */ T(V, {
									tone: "err",
									children: e("admin.content.errors", { n: t.errors.length })
								}) : null,
								t.warnings?.length ? /* @__PURE__ */ T(V, {
									tone: "warn",
									children: e("admin.content.warnings", { n: t.warnings.length })
								}) : null
							]
						}),
						(t.errors ?? []).map((e, t) => /* @__PURE__ */ T("div", {
							className: "k-error-text",
							children: e
						}, t)),
						(t.warnings ?? []).map((e, t) => /* @__PURE__ */ T("div", {
							className: "k-hint",
							style: { color: "var(--k-warn)" },
							children: e
						}, t))
					]
				}, t.file)) : /* @__PURE__ */ T(W, {
					title: e("admin.content.noPacks"),
					hint: e("admin.content.noPacksHint")
				}), (a.skipped ?? []).length ? /* @__PURE__ */ T("div", {
					className: "k-hint",
					children: e("admin.content.skipped", { list: a.skipped.join(", ") })
				}) : null]
			})
		}), /* @__PURE__ */ T(U, {
			title: e("admin.content.conflicts"),
			sub: e("admin.content.conflictsSub"),
			children: (a.conflicts ?? []).length ? /* @__PURE__ */ T("div", {
				className: "ka-list",
				children: a.conflicts.map((e, t) => /* @__PURE__ */ E("div", { children: [/* @__PURE__ */ T("div", {
					className: "k-mono",
					children: e.field
				}), /* @__PURE__ */ T("div", {
					className: "k-hint",
					children: j("admin.content.conflict", {
						winnerValue: /* @__PURE__ */ T("b", { children: e.winnerValue }),
						winner: e.winner,
						loserValue: e.loserValue,
						loser: e.loser
					})
				})] }, t))
			}) : /* @__PURE__ */ T(W, { title: e("admin.content.noConflicts") })
		})]
	});
}
function qo() {
	let e = M(), t = P("content_definitions"), n = I("content_definitions", {}, {
		enabled: t,
		staleTime: 3e4
	}), r = I("sync_status", {}, { refetchInterval: 15e3 }), [i, a] = x(null), o = y(() => {
		let e = /* @__PURE__ */ new Map();
		for (let t of n.data ?? []) if (t.kind === "item") {
			let n = Kr(t);
			n && e.set(t.name, n);
		}
		return new Map((n.data ?? []).map((t) => [t.guid, Kr(t, e) ?? t.name]));
	}, [n.data]), s = n.data?.find((e) => e.guid === i) ?? null;
	return /* @__PURE__ */ E("div", {
		className: "k-stack",
		style: { gap: 16 },
		children: [
			r.data ? /* @__PURE__ */ T(H, {
				tone: "info",
				title: e("admin.content.syncTitle"),
				children: r.data.content ? e("admin.content.syncServing", {
					state: r.data.content.state,
					fields: r.data.content.fields,
					bytes: r.data.content.bytes,
					parts: r.data.content.parts
				}) : e("admin.content.syncNothing")
			}) : null,
			/* @__PURE__ */ T(U, {
				title: e("admin.content.defsTitle"),
				sub: e("admin.content.defsSub"),
				flush: !0,
				children: /* @__PURE__ */ T(Gn, {
					"aria-label": e("admin.content.defsTable"),
					rows: n.data ?? [],
					rowKey: (e) => e.guid,
					selectedKey: i,
					onRowClick: (e) => a(i === e.guid ? null : e.guid),
					columns: [
						{
							key: "n",
							header: e("admin.spawn.colName"),
							sort: (e, t) => (o.get(e.guid) ?? e.name).localeCompare(o.get(t.guid) ?? t.name),
							render: (e) => /* @__PURE__ */ T(Kn, {
								name: o.get(e.guid) ?? e.name,
								id: e.name
							})
						},
						{
							key: "k",
							header: e("admin.items.colKind"),
							width: "80px",
							render: (e) => e.kind
						},
						{
							key: "b",
							header: e("admin.content.basedOn"),
							render: (e) => /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: e.base ?? "-"
							})
						},
						{
							key: "m",
							header: e("admin.content.mod"),
							width: "120px",
							render: (e) => e.mod ?? e.owner ?? ""
						},
						{
							key: "s",
							header: e("admin.players.colState"),
							width: "110px",
							render: (e) => /* @__PURE__ */ T(V, {
								tone: e.state === "registered" ? "ok" : "warn",
								children: e.state ?? "?"
							})
						}
					],
					empty: /* @__PURE__ */ T(W, { title: e(t ? "admin.content.noDefs" : "admin.content.noDefsOp") })
				})
			}),
			s ? /* @__PURE__ */ T(U, {
				title: o.get(s.guid) ?? s.name,
				sub: /* @__PURE__ */ E("span", {
					className: "k-mono",
					children: [
						s.name,
						" · ",
						s.guid,
						" · ",
						s.id
					]
				}),
				children: /* @__PURE__ */ T(qn, {
					value: s.definition ?? null,
					open: 2
				})
			}) : null
		]
	});
}
var Jo = Br(Vo);
// src/admin/pages/AdminSavesPage.tsx
function Yo(e) {
	switch (e) {
		case "Exact": return ["ok", A("admin.saves.matchExact")];
		case "None": return ["idle", A("admin.saves.matchNone")];
		case "Older": return ["warn", A("admin.saves.matchOlder")];
		case "Newer": return ["warn", A("admin.saves.matchNewer")];
		default: return ["idle", e];
	}
}
function Xo() {
	let e = M(), t = Y(), n = c(), r = Ft(), i = I("save_status", {}, { refetchInterval: 5e3 }), a = I("backup_list", {}, { refetchInterval: 15e3 }), [o, s] = x("before-changes"), l = () => Lt(n, t, ["save_status", "backup_list"]), u = F("save_now", {
		onSuccess: (t) => {
			G(e("admin.saves.saved", {
				revision: t.revision,
				ms: t.ms
			})), l();
		},
		onError: (e) => K(e)
	}), d = F("backup_create", {
		onSuccess: (t) => {
			G(e("admin.saves.backedUp", { folder: t.folder }), e("admin.saves.revisionDetail", { revision: t.revision })), l();
		},
		onError: (e) => K(e)
	}), f = F("backup_restore", {
		onSuccess: (t) => {
			G(e("admin.saves.restoreStaged", { folder: t.staged.folder }), e("admin.saves.restoreStagedDetail")), l();
		},
		onError: (e) => K(e)
	}), p = F("backup_cancel_restore", {
		onSuccess: (t) => {
			G(t.canceled ? e("admin.saves.restoreCanceled") : e("admin.saves.nothingStaged")), l();
		},
		onError: (e) => K(e)
	}), m = i.data, h = m?.pendingRestore ?? null, g = r.state?.players?.length ?? 0, _ = Yo(m?.dataMatch ?? ""), v = () => {
		let t = o.trim() || "launcher";
		if (!ri.test(t)) return K({ message: e("admin.saves.nameRule") });
		d.mutate({ name: t });
	}, y = async (t) => {
		await q({
			title: e("admin.saves.restoreTitle"),
			danger: !0,
			ok: e("admin.saves.stageRestore"),
			typed: "restore",
			body: /* @__PURE__ */ E("div", {
				className: "k-stack",
				children: [
					/* @__PURE__ */ T("p", { children: j("admin.saves.restoreBody", {
						next: /* @__PURE__ */ T("b", { children: e("admin.saves.restoreNext") }),
						folder: /* @__PURE__ */ T("b", { children: t.folder }),
						revision: t.revision ?? "?",
						time: Cr(t.created)
					}) }),
					/* @__PURE__ */ T("p", { children: g ? j("admin.saves.restoreLostPlayers", {
						lost: /* @__PURE__ */ T("b", { children: e("admin.saves.restoreLost") }),
						count: g,
						path: /* @__PURE__ */ T("span", {
							className: "k-mono",
							children: "kiln/backups/_before-restore-…"
						})
					}) : j("admin.saves.restoreLostBody", {
						lost: /* @__PURE__ */ T("b", { children: e("admin.saves.restoreLost") }),
						path: /* @__PURE__ */ T("span", {
							className: "k-mono",
							children: "kiln/backups/_before-restore-…"
						})
					}) }),
					/* @__PURE__ */ T("p", { children: e("admin.saves.restoreCancelHint") })
				]
			})
		}) && f.mutate({ name: t.folder || t.name });
	}, b = Qe().restartManagedServer, S = async () => {
		if (b && await q({
			title: e("admin.saves.restartTitle"),
			body: /* @__PURE__ */ E("p", { children: [g ? e("admin.saves.restartOnline", { count: g }) : "", h ? e("admin.saves.restartBodyRestore", { name: h }) : e("admin.saves.restartBody")] }),
			ok: e("admin.saves.restart"),
			danger: !0
		})) try {
			await b(t), G(e("admin.saves.restarting"));
		} catch (t) {
			K(t, e("admin.saves.restartFailed"));
		}
	}, C = [...a.data ?? []].reverse();
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.saves.title"),
			sub: e("admin.saves.sub"),
			actions: /* @__PURE__ */ T(L, {
				variant: "primary",
				loading: u.isPending,
				disabled: !r.session,
				onClick: () => u.mutate({}),
				title: e("admin.saves.saveNowTip"),
				children: e("admin.saves.saveNow")
			})
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ E(Vr, {
			ops: [
				"save_status",
				"save_now",
				"backup_list",
				"backup_create",
				"backup_restore"
			],
			what: e("admin.saves.what"),
			children: [
				h ? /* @__PURE__ */ T(H, {
					tone: "warn",
					title: e("admin.saves.stagedTitle", { name: h }),
					actions: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(L, {
						size: "s",
						loading: p.isPending,
						onClick: () => p.mutate({}),
						children: e("admin.saves.cancelRestore")
					}), b ? /* @__PURE__ */ T(L, {
						size: "s",
						variant: "danger",
						onClick: S,
						children: e("admin.saves.restartNow")
					}) : null] }),
					children: e("admin.saves.stagedBody")
				}) : null,
				m ? /* @__PURE__ */ T(U, {
					title: e("admin.saves.worldSave"),
					children: /* @__PURE__ */ T(Hn, { rows: [
						[e("admin.saves.kvWorld"), /* @__PURE__ */ T(w, { children: j("admin.saves.worldValue", {
							world: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: String(m.world ?? "-").slice(0, 13)
							}),
							revision: /* @__PURE__ */ T("b", { children: m.revision ?? "-" }),
							state: m.state ?? ""
						}) })],
						[e("admin.saves.kvLastSaved"), /* @__PURE__ */ E(w, { children: [
							Sr(m.lastSaved),
							" ",
							/* @__PURE__ */ T("span", {
								className: "k-muted",
								children: e("admin.saves.lastSavedDetail", {
									reason: String(m.lastReason ?? "").toLowerCase(),
									seconds: Math.round(m.autosaveSeconds ?? 0)
								})
							})
						] })],
						[e("admin.saves.kvModData"), /* @__PURE__ */ E(w, { children: [
							/* @__PURE__ */ T(V, {
								tone: _[0],
								children: m.dataMatch ?? "?"
							}),
							" ",
							_[1],
							" ",
							/* @__PURE__ */ T("span", {
								className: "k-muted",
								children: e("admin.saves.generation", { generation: m.dataGeneration ?? "-" })
							})
						] })],
						[e("admin.saves.kvSession"), /* @__PURE__ */ E(w, { children: [e("admin.saves.written", { written: m.written ?? 0 }), m.failed ? /* @__PURE__ */ T("b", {
							style: { color: "var(--k-err)" },
							children: e("admin.saves.failed", { failed: m.failed })
						}) : null] })]
					] })
				}) : i.error ? /* @__PURE__ */ T(H, {
					tone: "warn",
					children: i.error.message
				}) : null,
				/* @__PURE__ */ T(U, {
					title: e("admin.saves.backups"),
					sub: e("admin.saves.backupsSub"),
					actions: /* @__PURE__ */ E("span", {
						className: "ka-inline",
						children: [/* @__PURE__ */ T(R, {
							"aria-label": e("admin.saves.backupName"),
							maxLength: 64,
							value: o,
							onChange: (e) => s(e.target.value),
							onKeyDown: (e) => e.key === "Enter" && v(),
							style: { width: 200 }
						}), /* @__PURE__ */ T(L, {
							loading: d.isPending,
							onClick: v,
							children: e("admin.saves.backUp")
						})]
					}),
					flush: !0,
					children: /* @__PURE__ */ T(Gn, {
						"aria-label": e("admin.saves.backupsTable"),
						rows: C,
						rowKey: (e) => e.folder || e.name,
						rowHeight: 56,
						columns: [
							{
								key: "w",
								header: e("admin.saves.colWhen"),
								width: "190px",
								render: (e) => /* @__PURE__ */ E("div", { children: [/* @__PURE__ */ T("div", { children: Cr(e.created) }), /* @__PURE__ */ T("div", {
									className: "k-hint",
									children: Sr(e.created)
								})] }),
								sort: (e, t) => Date.parse(e.created) - Date.parse(t.created)
							},
							{
								key: "n",
								header: e("admin.saves.colBackup"),
								render: (t) => /* @__PURE__ */ E("div", { children: [
									/* @__PURE__ */ T(V, {
										tone: t.reason === "Backup" ? "ember" : "idle",
										children: t.name
									}),
									h === t.folder ? /* @__PURE__ */ E(w, { children: [" ", /* @__PURE__ */ T(V, {
										tone: "warn",
										children: e("admin.saves.staged")
									})] }) : null,
									/* @__PURE__ */ T("div", {
										className: "k-mono k-muted",
										children: t.folder
									})
								] })
							},
							{
								key: "r",
								header: e("admin.saves.colRevision"),
								width: "90px",
								align: "right",
								render: (e) => e.revision ?? "?"
							},
							{
								key: "s",
								header: e("admin.saves.colSize"),
								width: "150px",
								align: "right",
								render: (t) => /* @__PURE__ */ T("span", { children: t.dataBundles ? e("admin.saves.sizeData", {
									bytes: xr(t.bytes),
									files: t.files,
									data: t.dataBundles
								}) : e("admin.saves.size", {
									bytes: xr(t.bytes),
									files: t.files
								}) })
							},
							{
								key: "a",
								header: "",
								width: "190px",
								align: "right",
								render: (t) => /* @__PURE__ */ T(L, {
									size: "s",
									disabled: h === t.folder || f.isPending,
									onClick: () => y(t),
									children: e("admin.saves.restoreAtNext")
								})
							}
						],
						empty: /* @__PURE__ */ T(W, {
							title: e("admin.saves.empty"),
							hint: e("admin.saves.emptyHint")
						})
					})
				})
			]
		})
	] });
}
var Zo = Br(Xo);
// src/admin/pages/AdminCrashesPage.tsx
function Qo() {
	let e = M(), t = Y(), n = Ft(), r = P("crash_reports"), i = I("crash_reports", {}, { refetchInterval: 2e4 }), a = Qe().crashReportsFromDisk, o = s({
		queryKey: ["logs_crash_reports", t],
		queryFn: () => a(t),
		enabled: !!a && (!n.session || !r),
		retry: !1
	}), c = !!n.session && r, l = c ? i.data ?? [] : (o.data ?? []).map((e) => ({
		name: e.name,
		bytes: e.size,
		time: e.created
	})), [u, d] = x(null), [f, p] = x(""), m = u ?? l[0]?.name ?? null, h = I("crash_reports", { name: m ?? "" }, {
		enabled: !!m && c && !!i.data?.some((e) => e.name === m),
		staleTime: Infinity
	}), g = n.connectedAt, _ = h.data?.text ?? "", v = f ? _.split("\n").filter((e) => e.toLowerCase().includes(f.toLowerCase())).join("\n") : _;
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.crashes.title"),
			sub: e("admin.crashes.sub")
		}),
		/* @__PURE__ */ T(li, {}),
		!r && n.session ? /* @__PURE__ */ T(H, {
			tone: "ember",
			title: e("admin.common.needsNewer"),
			children: e("admin.crashes.noOp")
		}) : null,
		/* @__PURE__ */ E("div", {
			className: "ka-split list-left",
			children: [/* @__PURE__ */ T(U, {
				title: e("admin.crashes.reports"),
				sub: e("admin.crashes.reportsSub", { n: l.length }),
				flush: !0,
				children: /* @__PURE__ */ E("div", {
					className: "ka-pick",
					style: { padding: 6 },
					children: [
						l.map((t) => /* @__PURE__ */ E("button", {
							className: t.name === m ? "active" : "",
							onClick: () => d(t.name),
							children: [
								/* @__PURE__ */ T("span", {
									className: "k-mono",
									style: { fontSize: 12 },
									children: t.name.replace(/^crash-/, "").replace(/\.txt$/, "")
								}),
								g && Date.parse(t.time) > g - 6e4 ? /* @__PURE__ */ T(V, {
									tone: "warn",
									children: e("admin.crashes.new")
								}) : null,
								/* @__PURE__ */ T("small", { children: Sr(t.time) })
							]
						}, t.name)),
						l.length ? null : /* @__PURE__ */ T(W, {
							title: e("admin.crashes.empty"),
							hint: e("admin.crashes.emptyHint")
						}),
						i.error && r ? /* @__PURE__ */ T("div", {
							className: "k-error-text",
							style: { padding: 8 },
							children: i.error.message
						}) : null,
						o.error && (!r || !n.session) ? /* @__PURE__ */ T("div", {
							className: "k-hint",
							style: { padding: 8 },
							children: o.error.message
						}) : null
					]
				})
			}), m ? /* @__PURE__ */ E(U, {
				title: /* @__PURE__ */ T("span", {
					className: "k-mono",
					style: { fontSize: 13 },
					children: m
				}),
				sub: (() => {
					let e = l.find((e) => e.name === m);
					return e ? `${Cr(e.time)} · ${xr(e.bytes)}` : void 0;
				})(),
				actions: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(R, {
					"aria-label": e("admin.crashes.filterLabel"),
					placeholder: e("admin.crashes.filterPlaceholder"),
					value: f,
					onChange: (e) => p(e.target.value),
					style: { width: 180 }
				}), /* @__PURE__ */ T(L, {
					size: "s",
					variant: "ghost",
					disabled: !_,
					onClick: () => navigator.clipboard?.writeText(_),
					children: e("common.copy")
				})] }),
				children: [
					h.error ? /* @__PURE__ */ T(H, {
						tone: "warn",
						children: h.error.message
					}) : null,
					r ? null : /* @__PURE__ */ T("div", {
						className: "k-muted",
						children: e("admin.crashes.openOnServer", { name: m })
					}),
					r && !h.data && !h.error ? /* @__PURE__ */ T("div", {
						className: "k-muted",
						children: e("admin.players.reading")
					}) : null,
					h.data ? /* @__PURE__ */ T("pre", {
						className: "ka-pre",
						children: v
					}) : null,
					h.data && _.length >= 2e5 ? /* @__PURE__ */ T("div", {
						className: "k-hint",
						children: e("admin.crashes.first200")
					}) : null
				]
			}) : /* @__PURE__ */ T(U, { children: /* @__PURE__ */ T(W, { title: e("admin.crashes.pick") }) })]
		})
	] });
}
var $o = Br(Qo), es = [
	[
		"players",
		() => A("admin.pages.players"),
		vi
	],
	[
		"map",
		() => A("admin.pages.map"),
		ka
	],
	[
		"zones",
		() => A("admin.pages.zones"),
		Na
	],
	[
		"items",
		() => A("admin.pages.items"),
		La
	],
	[
		"spawn",
		() => A("admin.pages.spawn"),
		Ba
	],
	[
		"world",
		() => A("admin.pages.world"),
		io
	],
	[
		"combat",
		() => A("admin.pages.combat"),
		fo
	],
	[
		"creatures",
		() => A("admin.pages.creatures"),
		xo
	],
	[
		"gamedata",
		() => A("admin.pages.gamedata"),
		Do
	],
	[
		"permissions",
		() => A("admin.pages.permissions"),
		zo
	],
	[
		"content",
		() => A("admin.pages.content"),
		Jo
	],
	[
		"saves",
		() => A("admin.pages.saves"),
		Zo
	],
	[
		"crashes",
		() => A("admin.pages.crashes"),
		$o
	]
];
Qt();
function ts(e) {
	return `/servers/${encodeURIComponent(e)}/admin`;
}
function ns() {
	return /* @__PURE__ */ T(t, {});
}
var rs = rn;
function is({ serverId: t }) {
	let i = a(), o = t ?? i.id;
	return o ? /* @__PURE__ */ T(jt, {
		serverId: o,
		children: /* @__PURE__ */ T(r, { children: /* @__PURE__ */ E(n, {
			element: /* @__PURE__ */ T(ns, {}),
			children: [
				/* @__PURE__ */ T(n, {
					index: !0,
					element: /* @__PURE__ */ T(e, {
						to: `${ts(o)}/players`,
						replace: !0
					})
				}),
				es.map(([e, , t]) => /* @__PURE__ */ T(n, {
					path: e,
					element: /* @__PURE__ */ T(t, {})
				}, e)),
				rs.map(([t, r]) => /* @__PURE__ */ T(n, {
					path: t,
					element: /* @__PURE__ */ T(e, {
						to: `/servers/${encodeURIComponent(o)}/${r}`,
						replace: !0
					})
				}, t)),
				/* @__PURE__ */ T(n, {
					path: "*",
					element: /* @__PURE__ */ T(e, {
						to: `${ts(o)}/players`,
						replace: !0
					})
				})
			]
		}) })
	}) : null;
}
// src/admin/pages/AdminConfigsPage.tsx
var as = /^[A-Za-z0-9_]{1,64}(\.[A-Za-z0-9_]{1,64}){0,8}$/;
function os(e) {
	return {
		format: 1,
		source: "kiln",
		file: e.file,
		valueFormat: "jsonc",
		id: e.id,
		mod: e.mod,
		modVersion: null,
		kind: e.kind,
		written: e.loadedAt ?? null,
		kiln: null,
		sections: e.sections.map((e) => ({
			path: e.path,
			comment: e.comment ?? null,
			synced: !!e.synced
		})),
		entries: e.entries.map((e) => ({
			path: e.path,
			type: e.type,
			comment: e.comment ?? null,
			default: e.default,
			requiresRestart: !!e.requiresRestart,
			synced: !!e.synced,
			min: e.min,
			max: e.max,
			step: e.step,
			options: e.options
		}))
	};
}
function ss(e) {
	let t = {};
	for (let n of e.entries) {
		let e = n.path.split("."), r = t;
		for (let t of e.slice(0, -1)) r = r[t] ??= {};
		r[e[e.length - 1]] = n.fileValue === void 0 ? n.value : n.fileValue;
	}
	return t;
}
function cs(e, t) {
	let n = e.id === e.mod ? t?.find((t) => t.id === e.mod)?.name : void 0;
	return n && n.length <= 24 ? n : e.id.charAt(0).toUpperCase() + e.id.slice(1);
}
var ls = (e) => Array.isArray(e) ? e.join(", ") || A("admin.configs.empty") : typeof e == "boolean" ? A(e ? "admin.configs.on" : "admin.configs.off") : String(e);
function us() {
	let e = M(), t = Y(), n = c(), r = I("config_list", {}, { refetchInterval: 1e4 }), i = I("mods", {}, {
		enabled: P("mods"),
		staleTime: 3e4
	}), [a, o] = x(null), [s, l] = x(""), [u, d] = x([]), f = r.data?.find((e) => e.id === (a ?? r.data?.[0]?.id)) ?? null, p = (e) => n.setQueriesData({ queryKey: [
		"admin",
		t,
		"config_list"
	] }, (t) => t?.map((t) => t.id === e.id ? e : t)), m = F("config_set"), h = F("config_reload", { onSuccess: (t) => {
		p(t), G(e("admin.configs.reread", { id: t.id }));
	} }), g = y(() => f ? os(f) : null, [f]), _ = y(() => f ? ss(f) : null, [f]), v = f?.entries.filter((e) => e.fileValue !== void 0 && JSON.stringify(e.fileValue) !== JSON.stringify(e.value)).map((e) => e.path) ?? [], b = (t, n) => {
		if (!f || !g) return;
		let r = g.entries.find((e) => e.path === t), i = as.test(t) ? r ? tr(r, n) : null : e("admin.configs.badPath"), a = Array.isArray(n) && n.length > 500 ? e("admin.configs.listMax") : typeof n == "string" && n.length > 2e3 ? e("admin.configs.textMax") : null, o = i ?? a;
		d((e) => [...e.filter((e) => e.path !== t), ...o ? [{
			path: t,
			message: o
		}] : []]), o || m.mutate({
			id: f.id,
			path: t,
			value: n
		}, {
			onSuccess: (r) => {
				p(r);
				let i = r.entries.find((e) => e.path === t);
				G(`${r.id}: ${t} = ${ls(i?.fileValue ?? i?.value ?? n)}`, i?.requiresRestart && i.fileValue !== void 0 ? e("admin.configs.appliesNext") : void 0);
			},
			onError: (e) => d((n) => [...n.filter((e) => e.path !== t), {
				path: t,
				message: e.message
			}])
		});
	}, S = String(f?.file ?? "").replace(/^.*[\\/]kiln[\\/]/, "kiln/").replace(/\\/g, "/");
	return /* @__PURE__ */ E(Bn, { children: [
		/* @__PURE__ */ T(zn, {
			title: e("admin.configs.title"),
			sub: e("admin.configs.sub")
		}),
		/* @__PURE__ */ T(li, {}),
		/* @__PURE__ */ T(Vr, {
			ops: ["config_list", "config_set"],
			what: e("admin.configs.title"),
			children: /* @__PURE__ */ E("div", {
				className: "k-stack",
				style: { gap: 18 },
				children: [
					r.data?.length ? /* @__PURE__ */ T(Un, {
						"aria-label": e("admin.configs.tabs"),
						value: f?.id ?? "",
						onChange: (e) => {
							o(e), d([]);
						},
						items: r.data.map((t) => ({
							id: t.id,
							count: t.entries.length,
							label: /* @__PURE__ */ E(w, { children: [cs(t, i.data), t.error ? /* @__PURE__ */ T("span", {
								className: "k-tone-err",
								children: e("admin.configs.tabError")
							}) : t.warnings?.length ? /* @__PURE__ */ T("span", {
								className: "k-tone-warn",
								title: t.warnings.join(" · "),
								children: e("admin.configs.tabCorrected")
							}) : null] })
						}))
					}) : null,
					r.data && !r.data.length ? /* @__PURE__ */ T(W, {
						title: e("admin.configs.none"),
						hint: e("admin.configs.noneHint")
					}) : null,
					r.error ? /* @__PURE__ */ T("div", {
						className: "k-error-text",
						children: r.error.message
					}) : null,
					f && g && _ ? /* @__PURE__ */ T(U, {
						title: cs(f, i.data),
						sub: /* @__PURE__ */ T(w, { children: f.loadedAt ? j("admin.configs.cardSubRead", {
							id: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: f.id
							}),
							mod: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: f.mod
							}),
							kind: f.kind,
							file: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: S
							}),
							ago: Sr(f.loadedAt)
						}) : j("admin.configs.cardSub", {
							id: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: f.id
							}),
							mod: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: f.mod
							}),
							kind: f.kind,
							file: /* @__PURE__ */ T("span", {
								className: "k-mono",
								children: S
							})
						}) }),
						actions: /* @__PURE__ */ E(w, { children: [/* @__PURE__ */ T(R, {
							"aria-label": e("admin.configs.filterLabel"),
							placeholder: e("admin.configs.filterPlaceholder"),
							value: s,
							onChange: (e) => l(e.target.value),
							style: { width: 160 }
						}), /* @__PURE__ */ T(L, {
							size: "s",
							variant: "ghost",
							loading: h.isPending,
							onClick: () => h.mutate({ id: f.id }),
							title: e("admin.configs.rereadTip"),
							children: e("admin.configs.rereadFile")
						})] }),
						children: /* @__PURE__ */ E("div", {
							className: "k-stack",
							style: { gap: 12 },
							children: [
								f.error ? /* @__PURE__ */ T(H, {
									tone: "err",
									title: e("admin.configs.errorTitle"),
									children: e("admin.configs.errorBody", { error: f.error })
								}) : null,
								f.warnings?.length ? /* @__PURE__ */ T(H, {
									tone: "warn",
									title: e("admin.configs.correctedTitle"),
									children: f.warnings.join(" · ")
								}) : null,
								/* @__PURE__ */ T(ir, {
									schema: g,
									values: _,
									onChange: b,
									onReset: (e) => {
										let t = g.entries.find((t) => t.path === e);
										t && b(e, t.default);
									},
									errors: u,
									pending: v,
									disabled: m.isPending,
									filter: s
								}),
								v.length ? /* @__PURE__ */ T(H, {
									tone: "ember",
									children: e("admin.configs.pendingNote")
								}) : null
							]
						})
					}) : r.data?.length || !r.data ? /* @__PURE__ */ T(W, {
						title: r.data ? e("admin.configs.pick") : e("admin.map.loading"),
						hint: e("admin.configs.pickHint")
					}) : null
				]
			})
		})
	] });
}
var ds = Br(us), fs = {
	maxFrame: 16 << 20,
	maxClientFrame: 1 << 20,
	inflight: 64,
	pingS: 15,
	idleS: 45
}, ps = /* @__PURE__ */ new Set([4401, 4403]);
function ms(e) {
	return e === "mod_test" || e === "update_stage" ? 180 : e === "backup_create" ? 90 : e === "save_now" ? 60 : e.startsWith("mod_") ? 45 : 30;
}
function hs(e) {
	switch (e) {
		case "busy": return "busy";
		case "timeout": return "timeout";
		case "auth_failed":
		case "auth_required": return "auth_failed";
		case "shutting_down": return "admin_unavailable";
		case "unknown_topic":
		case "bad_frame": return "invalid_argument";
		case "version": return "unsupported";
		case "internal": return "internal";
		default: return "op_failed";
	}
}
var $ = (e, t, n) => n === void 0 ? {
	code: e,
	message: t
} : {
	code: e,
	message: t,
	detail: n
}, gs = (e) => e.replace(/[^A-Za-z0-9_-]/g, "_").slice(0, 48), _s = (e) => typeof e == "object" && !!e && !Array.isArray(e), vs = class e {
	ws;
	hello = null;
	ready;
	limits = fs;
	closed = !1;
	next = 1;
	waiting = /* @__PURE__ */ new Map();
	sentAt = Date.now();
	receivedAt = Date.now();
	keepalive = null;
	onPush = () => void 0;
	onError = () => void 0;
	onClose = () => void 0;
	constructor(e) {
		this.ws = e;
	}
	static open(t, n, r) {
		return new Promise((i, a) => {
			let o;
			try {
				o = new t(n);
			} catch (e) {
				a($("admin_unavailable", A("api.adminWs.unreachable", { reason: (e instanceof Error ? e.message : String(e)).split(n).join(n.replace(/\?.*$/, "")) })));
				return;
			}
			let s = new e(o), c = "handshake", l = (e) => {
				if (c === "handshake") {
					c = "done", clearTimeout(u), s.closed = !0;
					try {
						o.close();
					} catch {}
					a(e);
				}
			}, u = setTimeout(() => l($("admin_unavailable", A("api.adminWs.handshake", { seconds: Math.round(r / 100) / 10 }))), r);
			o.binaryType = "arraybuffer", o.addEventListener("error", () => l($("admin_unavailable", A("api.adminWs.unreachable", { reason: n.replace(/\?.*$/, "") })))), o.addEventListener("close", (e) => {
				if (c === "handshake") {
					let t = e.reason || A("api.adminWs.closedBy", { code: e.code });
					l($(ps.has(e.code) ? "auth_failed" : "admin_unavailable", A("api.adminWs.closedEarly", { reason: t })));
				} else c === "open" && (c = "done", s.shut(e.reason || A("api.adminWs.closedBy", { code: e.code }), ps.has(e.code)));
			}), o.addEventListener("message", (e) => {
				let t = bs(e.data);
				if (s.receivedAt = Date.now(), c === "handshake") {
					if (!t) return l($("admin_unavailable", A("api.adminWs.closedEarly", { reason: "bad frame" })));
					if (t.t === "hello") {
						if (typeof t.proto == "string" && t.proto !== "kap/1") return l($("unsupported", `the server speaks ${t.proto}, this page kap/1`));
						s.hello = t;
					} else if (t.t === "ready") {
						if (!Array.isArray(t.ops) || !Array.isArray(t.topics)) return l($("admin_unavailable", A("api.adminWs.closedEarly", { reason: "bad ready" })));
						c = "open", clearTimeout(u), s.ready = t, s.limits = ys(t.limits), s.startKeepalive(), i(s);
					} else t.t === "error" && l($(hs(t.code), String(t.message ?? t.code)));
				} else if (c === "open") {
					if (!t) {
						c = "done", s.shut("bad frame from the server", !1);
						try {
							o.close();
						} catch {}
					} else if (t.t === "bye") {
						c = "done", s.shut(`bye: ${String(t.reason ?? "")}`, !1);
						try {
							o.close();
						} catch {}
					} else s.frame(t);
				}
			});
		});
	}
	frame(e) {
		switch (e.t) {
			case "res": {
				let t = this.waiting.get(e.id);
				t && this.settle(e.id, t).resolve(e);
				break;
			}
			case "error": {
				let t = typeof e.id == "number" ? this.waiting.get(e.id) : void 0;
				t ? this.settle(e.id, t).reject($(hs(e.code), String(e.message ?? e.code))) : this.onError(`${String(e.code ?? "")}: ${String(e.message ?? "")}`);
				break;
			}
			case "push":
				this.onPush(String(e.topic), Number(e.seq), e.data ?? null);
				break;
			case "ping": this.send({
				t: "pong",
				id: e.id,
				...e.ts === void 0 ? {} : { ts: e.ts }
			});
		}
	}
	settle(e, t) {
		return this.waiting.delete(e), t.timer && clearTimeout(t.timer), t;
	}
	send(e) {
		this.ws.send(JSON.stringify(e)), this.sentAt = Date.now();
	}
	request(e, t) {
		if (this.closed) return Promise.reject($("admin_unavailable", A("api.adminWs.connectionClosed")));
		let n = this.next++, r = e(n), i = JSON.stringify(r), a = new TextEncoder().encode(i).length;
		return a > this.limits.maxClientFrame ? Promise.reject($("invalid_argument", A("api.adminWs.tooLarge", {
			bytes: a,
			max: this.limits.maxClientFrame
		}))) : new Promise((e, r) => {
			let a = {
				resolve: e,
				reject: r,
				timer: null
			};
			a.timer = setTimeout(() => {
				this.waiting.get(n) === a && this.waiting.delete(n), r($("timeout", A("api.adminWs.timeout", { seconds: Math.round(t / 1e3) })));
			}, t), this.waiting.set(n, a);
			try {
				this.ws.send(i), this.sentAt = Date.now();
			} catch (e) {
				this.settle(n, a).reject($("admin_unavailable", e instanceof Error ? e.message : String(e)));
			}
		});
	}
	startKeepalive() {
		let e = Math.min(Math.max(this.limits.pingS, 1), 15) * 1e3, t = Math.max(this.limits.idleS * 1e3, e * 2), n = Math.min(Math.max(e / 4, 50), 1e3);
		this.keepalive = setInterval(() => {
			if (this.closed) return;
			let n = Date.now();
			if (n - this.receivedAt > t) {
				this.shut(A("api.adminWs.silent", { seconds: Math.round(t / 1e3) }), !1);
				try {
					this.ws.close();
				} catch {}
			} else if (n - this.sentAt >= e) try {
				this.send({
					t: "ping",
					id: this.next++,
					ts: n
				});
			} catch {
				this.shut("send failed", !1);
			}
		}, n);
	}
	shut(e, t) {
		if (!this.closed) {
			this.closed = !0, this.keepalive && clearInterval(this.keepalive);
			for (let [e, t] of [...this.waiting]) this.settle(e, t).reject($("admin_unavailable", A("api.adminWs.connectionClosed")));
			this.onClose(e, t);
		}
	}
	end(e) {
		if (!this.closed) {
			this.closed = !0, this.keepalive && clearInterval(this.keepalive);
			for (let [e, t] of [...this.waiting]) this.settle(e, t).reject($("admin_unavailable", A("api.adminWs.connectionClosed")));
			try {
				this.send({
					t: "bye",
					reason: e
				});
			} catch {}
			try {
				this.ws.close(1e3, e);
			} catch {}
		}
	}
};
function ys(e) {
	let t = { ...fs };
	if (_s(e)) for (let n of Object.keys(t)) {
		let r = e[n];
		typeof r == "number" && Number.isFinite(r) && r >= 0 && (t[n] = r);
	}
	return t;
}
function bs(e) {
	let t;
	if (typeof e == "string") t = e;
	else if (e instanceof ArrayBuffer) t = new TextDecoder().decode(e);
	else if (ArrayBuffer.isView(e)) t = new TextDecoder().decode(e);
	else return null;
	try {
		let e = JSON.parse(t);
		return _s(e) && typeof e.t == "string" ? e : null;
	} catch {
		return null;
	}
}
function xs(e) {
	let t = e.WebSocket ?? globalThis.WebSocket, [n, r] = e.backoffMs ?? [1e3, 3e4], i = e.handshakeMs ?? 1e4, a = e.callTimeoutMs ?? ((e) => (ms(e) + 10) * 1e3), o = /* @__PURE__ */ new Map(), s = /* @__PURE__ */ new Set(), c = 0, l = (e, t) => {
		for (let n of [...s]) try {
			n({
				session: e.id,
				serverId: e.serverId,
				event: t
			});
		} catch (e) {
			console.error("admin ws listener", e);
		}
	};
	async function u(n) {
		let r;
		try {
			r = typeof e.url == "function" ? await e.url(n) : e.url;
		} catch (e) {
			throw We(e) ? e : $("admin_unavailable", e instanceof Error ? e.message : String(e));
		}
		return vs.open(t, r, i);
	}
	let d = (e, t) => {
		let n = t.ready, r = t.hello, i = {
			session: e.id,
			serverId: e.serverId,
			transport: "ws",
			ops: n.ops.map(String),
			topics: n.topics.map(String),
			kiln: typeof r?.kiln == "string" ? r.kiln : null,
			server: _s(r?.server) ? r.server : {}
		};
		return _s(n.restart) && (i.restart = n.restart), i;
	}, f = (e) => ({
		kind: "ready",
		transport: "ws",
		ops: e.ops,
		topics: e.topics,
		...e.restart ? { restart: e.restart } : {}
	});
	function p(e, t) {
		e.link = t, t.onPush = (n, r, i) => {
			e.link === t && l(e, {
				kind: "push",
				topic: n,
				seq: r,
				data: i
			});
		}, t.onError = (n) => {
			e.link === t && l(e, {
				kind: "error",
				message: n
			});
		}, t.onClose = (r, i) => {
			if (!(e.link !== t || e.stopped)) {
				if (e.link = null, i) return m(e, r);
				l(e, {
					kind: "closed",
					reason: r,
					retry: !0
				}), h(e, n);
			}
		};
	}
	function m(e, t) {
		e.stopped = !0, o.get(e.id) === e && o.delete(e.id), l(e, {
			kind: "closed",
			reason: t,
			retry: !1
		});
	}
	function h(e, t) {
		e.retry = setTimeout(async () => {
			if (e.retry = null, e.stopped) return;
			let n;
			try {
				n = await u(e.serverId);
			} catch (n) {
				if (e.stopped) return;
				let i = We(n) ? n : $("admin_unavailable", String(n));
				return i.code === "auth_failed" ? m(e, i.message) : h(e, Math.min(t * 2, r));
			}
			if (e.stopped) return n.end("disconnect");
			let i = [];
			n.onPush = (e, t, n) => i.push([
				e,
				t,
				n
			]);
			for (let t of e.topics) try {
				let r = await n.request((e) => ({
					t: "sub",
					id: e,
					topic: t
				}), a("sub"));
				r.ok === !1 && l(e, {
					kind: "error",
					message: `re-subscribing ${t}: ${String(r.error ?? r.code)}`
				});
			} catch (n) {
				l(e, {
					kind: "error",
					message: `re-subscribing ${t}: ${We(n) ? n.message : String(n)}`
				});
			}
			if (e.stopped) return n.end("disconnect");
			if (n.closed) return h(e, Math.min(t * 2, r));
			e.info = d(e, n), p(e, n), l(e, f(e.info));
			for (let [t, n, r] of i) l(e, {
				kind: "push",
				topic: t,
				seq: n,
				data: r
			});
		}, t);
	}
	let g = (e) => {
		let t = o.get(e);
		if (!t) throw $("not_found", A("api.adminWs.noSession", { session: e }));
		return t;
	}, _ = (e) => {
		if (!e.link || e.link.closed) throw $("admin_unavailable", A("api.adminWs.reconnecting"));
		return e.link;
	};
	async function v(e, t, n) {
		let r = g(e);
		if (n) {
			if (!r.info.topics.includes(t)) throw $("invalid_argument", A("api.adminWs.noTopic", { topic: t }));
			r.topics.add(t);
		} else r.topics.delete(t);
		if (!r.link || r.link.closed) return;
		let i = await r.link.request((e) => ({
			t: n ? "sub" : "unsub",
			id: e,
			topic: t
		}), a("sub"));
		if (i.ok === !1) throw $(hs(i.code), String(i.error ?? A("api.adminWs.noTopic", { topic: t })));
	}
	let y = {
		name: "websocket",
		kinds: [],
		async connect(e) {
			for (let t of o.values()) if (t.serverId === e && !t.stopped) return t.info;
			let t = await u(e);
			for (let n of o.values()) if (n.serverId === e && !n.stopped) return t.end("disconnect"), n.info;
			let n = {
				id: `ws-${gs(e)}-${++c}`,
				serverId: e,
				info: null,
				link: null,
				topics: /* @__PURE__ */ new Set(),
				stopped: !1,
				retry: null
			};
			return n.info = d(n, t), o.set(n.id, n), p(n, t), l(n, f(n.info)), n.info;
		},
		async disconnect(e) {
			let t = g(e);
			t.stopped = !0, o.delete(t.id), t.retry && clearTimeout(t.retry), t.retry = null;
			let n = t.link;
			t.link = null, n?.end("disconnect"), l(t, {
				kind: "closed",
				reason: "disconnected",
				retry: !1
			});
		},
		async call(e, t, n) {
			let r = await _(g(e)).request((e) => ({
				t: "req",
				id: e,
				op: t,
				args: n ?? {}
			}), a(t));
			if (r.ok === !0) return r.data ?? null;
			throw $(hs(r.code), typeof r.error == "string" && r.error ? r.error : A("api.adminWs.opFailed"), {
				op: t,
				transport: "ws",
				kapCode: r.code ?? null
			});
		},
		subscribe: (e, t) => v(e, t, !0),
		unsubscribe: (e, t) => v(e, t, !1),
		async listen(e) {
			return s.add(e), () => {
				s.delete(e);
			};
		},
		async disconnectAll() {
			for (let e of [...o.keys()]) await y.disconnect(e).catch(() => void 0);
		}
	};
	return y;
}
export { es as ADMIN_PAGES, at as ADMIN_STALE_MS, fo as AdminCombatPage, ds as AdminConfigsPage, Jo as AdminContentPage, $o as AdminCrashesPage, xo as AdminCreaturesPage, Do as AdminGameDataPage, La as AdminItemsPage, ka as AdminMapPage, zo as AdminPermissionsPage, vi as AdminPlayersPage, jt as AdminProvider, is as AdminRoutes, Zo as AdminSavesPage, Ba as AdminSpawnPage, io as AdminWorldPage, Na as AdminZonesPage, Dr as ConfirmHost, Ke as KAP_PROTOCOL, li as OfflineNote, Ln as Toaster, wt as acquireAdmin, ts as adminBase, Qe as adminHost, Ze as adminTransport, yi as assetsRoot, kt as callAdmin, Ye as configureAdmin, xs as createWebSocketTransport, Re as currentLanguage, Lt as invalidateAdmin, We as isKilnError, Rt as kapOps, je as languages, Ot as needsNewer, vt as onAdminState, yt as onAdminTopic, ms as opBudgetS, Et as reconnectAdmin, Ae as registerLocale, Tt as releaseAdmin, Le as setLanguage, A as t, Ge as toKilnError, Fn as toast, F as useAdminCall, It as useAdminCaller, et as useAdminHub, I as useAdminQuery, Mt as useAdminServerId, Ft as useAdminState, P as useHasOp, Nt as useInAdminProvider, zt as useKap, M as useT };

