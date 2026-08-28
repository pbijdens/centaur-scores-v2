<script lang="ts">
  import { onMount } from 'svelte'
  import { ApiClient, login } from './lib/api'
  import {
    accounts,
    categories,
    childTenants,
    competitions,
    currentTenant,
    matches,
    participantLists,
    personalBestStatus,
    profile,
    templates,
    tenants,
    loadData as loadAllData,
    loadTenants as fetchTenantsList,
    loadMatchesList as fetchMatchesList,
    loadCompetitionsList as fetchCompetitionsList,
    loadAccounts as fetchAccountsList,
    loadChildTenants as fetchChildTenantsList,
    refreshCategories as fetchCategoriesList,
    refreshParticipantLists as fetchParticipantListsList,
    refreshPersonalBestStatus as fetchPersonalBestStatus,
    refreshTemplates as fetchTemplatesList,
    resetMatchesAndCompetitions
  } from './lib/data'
  import { labelForError } from './lib/errors'
  import { translationsFor } from './lib/i18n'
  import { navigateTo, resolveRoute } from './lib/router'
  import type { Account, Category, Competition, Language, Match, MatchTemplate, ParticipantList, ParticipantListSummary, Tenant, View } from './lib/types'
  import AccountEditView from './lib/views/AccountEditView.svelte'
  import AccountsView from './lib/views/AccountsView.svelte'
  import AppHeader from './lib/AppHeader.svelte'
  import CategoriesView from './lib/views/CategoriesView.svelte'
  import CategoryDetailView from './lib/views/CategoryDetailView.svelte'
  import CompetitionDetailView from './lib/views/CompetitionDetailView.svelte'
  import CompetitionResultsView from './lib/views/CompetitionResultsView.svelte'
  import CompetitionsView from './lib/views/CompetitionsView.svelte'
  import HomeView from './lib/views/HomeView.svelte'
  import LoginView from './lib/views/LoginView.svelte'
  import LiveScoringView from './lib/views/LiveScoringView.svelte'
  import MatchDetailView from './lib/views/MatchDetailView.svelte'
  import MatchDevicesView from './lib/views/MatchDevicesView.svelte'
  import MatchesView from './lib/views/MatchesView.svelte'
  import MatchMetadataEditView from './lib/views/MatchMetadataEditView.svelte'
  import MatchParticipantView from './lib/views/MatchParticipantView.svelte'
  import MatchQrCodesView from './lib/views/MatchQrCodesView.svelte'
  import MatchResultsScopeView from './lib/views/MatchResultsScopeView.svelte'
  import ParticipantListDetailView from './lib/views/ParticipantListDetailView.svelte'
  import ParticipantListsView from './lib/views/ParticipantListsView.svelte'
  import ParticipantMemberView from './lib/views/ParticipantMemberView.svelte'
  import PersonalBestClassifiersView from './lib/views/PersonalBestClassifiersView.svelte'
  import PersonalBestDisciplinesView from './lib/views/PersonalBestDisciplinesView.svelte'
  import PersonalBestExportConfigView from './lib/views/PersonalBestExportConfigView.svelte'
  import PersonalBestImportConfigView from './lib/views/PersonalBestImportConfigView.svelte'
  import PersonalBestLogView from './lib/views/PersonalBestLogView.svelte'
  import PersonalBestView from './lib/views/PersonalBestView.svelte'
  import ProfileView from './lib/views/ProfileView.svelte'
  import TemplateEditView from './lib/views/TemplateEditView.svelte'
  import TemplatesView from './lib/views/TemplatesView.svelte'
  import TenantEditView from './lib/views/TenantEditView.svelte'
  import TenantsView from './lib/views/TenantsView.svelte'

  const rootTenant = '00000000-0000-0000-0000-000000000001'
  let loggedIn = localStorage.getItem('centaur-token') !== null
  let token = localStorage.getItem('centaur-token') ?? ''
  let tenant = localStorage.getItem('centaur-tenant') ?? rootTenant
  let tenantsLoading = false
  let tenantsError = ''
  let username = ''
  let password = ''
  let language = (localStorage.getItem('centaur-language') ?? 'en') as Language
  let view: View = 'home'
  let selectedMatch: Match | null = null
  let matchSourceList: ParticipantList | null = null
  let selectedList: ParticipantList | null = null
  let loginError = ''
  let loading = false
  let selectedTenantId: string | null = null
  let selectedAccountId: string | null = null
  let selectedCategoryId: string | null = null
  let selectedListId: string | null = null
  let selectedMemberId: string | null = null
  let selectedTemplateId: string | null = null
  let selectedMatchId: string | null = null
  let selectedParticipantId: string | null = null
  let selectedCompetitionId: string | null = null
  let selectedCompetition: Competition | null = null
  let narrowcastScope: string | null = null
  let selectedResultsScope: string | null = null

  $: t = translationsFor(language)
  $: isAdmin = $profile?.authorization === 'Administrator'
  $: canManage = isAdmin || $profile?.authorization === 'Manager'
  $: headerUsername = $profile?.displayName || $profile?.username || username
  $: showPersonalBestButton = $personalBestStatus !== null && (!$personalBestStatus.enabled || $personalBestStatus.ownedHere)
  $: homeQuickLinks = (
    [['participants', t.participants], ['categories', t.categories], ['templates', t.templates]] as [string, string][]
  ).concat(isAdmin ? [['accounts', t.accounts], ['tenants', t.tenants]] : [])
    .concat(showPersonalBestButton ? [['personal-best', t.personalBest]] : [])
  $: selectedCategory = $categories.find((category) => category.id === selectedCategoryId) ?? null
  $: selectedMember = selectedMemberId && selectedMemberId !== 'new' ? selectedList?.members.find((member) => member.id === selectedMemberId) ?? null : null
  $: selectedTemplate = $templates.find((template) => template.id === selectedTemplateId) ?? null
  $: selectedParticipant = selectedMatch?.participants?.find((participant) => participant.id === selectedParticipantId) ?? null
  $: document.title = loggedIn && $currentTenant?.name ? `CentaurScores - ${$currentTenant.name}` : 'CentaurScores'

  const api = new ApiClient(() => token, signOut)

  async function loadTenants() {
    tenantsLoading = true
    tenantsError = ''
    try {
      const result = await fetchTenantsList()
      if (result.length > 0 && !result.some((item) => item.id === tenant)) tenant = result[0].id
    } catch {
      tenants.set([])
      tenantsError = t.tenantsLoadError
    } finally { tenantsLoading = false }
  }

  async function loadData() {
    if (!token) return
    loading = true
    try {
      await loadAllData(api)
      applyRoute()
    } catch { resetMatchesAndCompetitions() } finally { loading = false }
  }

  async function signIn() {
    try {
      const result = await login(username, password, tenant)
      token = result.token; loggedIn = true; loginError = ''
      localStorage.setItem('centaur-token', token); localStorage.setItem('centaur-tenant', tenant); await loadData()
      navigate('/')
    } catch (error) { loginError = labelForError(error, t, 'signInError') }
  }

  function signOut() {
    localStorage.removeItem('centaur-token'); token = ''; loggedIn = false; username = ''; password = ''; loginError = ''; selectedMatch = null; navigate('/login', true)
  }

  function setLanguage(value: string) { language = value as Language; localStorage.setItem('centaur-language', value) }

  function openMatch(match: { id: string }) { navigate(`/matches/${match.id}`) }

  async function loadSelectedMatch(id: string) {
    const [match, participants] = await Promise.all([
      api.fetchMatch(id),
      api.fetchMatchParticipants(id)
    ])
    selectedMatch = { ...match, participants }
    matchSourceList = match.participantListId ? await api.fetchParticipantList(match.participantListId) : null
  }

  async function refreshSelectedMatch() {
    if (selectedMatchId) await loadSelectedMatch(selectedMatchId)
  }

  async function loadSelectedCompetition(id: string) {
    selectedCompetition = await api.fetchCompetition(id)
  }

  async function refreshSelectedCompetition() {
    if (selectedCompetitionId) await loadSelectedCompetition(selectedCompetitionId)
  }

  function openCompetition(competition: Competition) { navigate(`/competitions/${competition.id}`) }

  async function loadCompetitionsList() {
    await fetchCompetitionsList(api)
  }

  function onCompetitionDeleted() {
    navigate('/competitions')
    loadCompetitionsList()
  }

  function toggleSelectedMatch() {
    if (!selectedMatch) return
    api.updateMatch(selectedMatch.id, { ...selectedMatch, isOpen: !selectedMatch.isOpen }).then(() => { refreshSelectedMatch(); loadMatchesList() })
  }

  async function loadMatchesList() {
    await fetchMatchesList(api)
  }

  function deactivateAllMatches() {
    api.deactivateAllMatches().then(loadMatchesList)
  }

  function openParticipant(participantId: string) {
    if (selectedMatchId) navigate(`/matches/${selectedMatchId}/participants/${participantId}`)
  }

  function returnToSelectedMatch() {
    if (!selectedMatchId) return
    navigate(`/matches/${selectedMatchId}`)
    window.location.reload()
  }

  function onMatchDeleted() {
    navigate('/matches')
    loadMatchesList()
  }

  function openTenant(childTenant: Tenant) { navigate(`/tenants/${childTenant.id}`) }

  async function loadChildTenants() {
    await fetchChildTenantsList(api)
  }

  async function createChildTenant(name: string) {
    await api.createChildTenant({ name, parentTenantId: tenant })
    await loadChildTenants()
  }

  function onTenantDeleted() {
    navigate('/tenants')
    loadChildTenants()
  }

  function openAccount(account: Account) { navigate(`/accounts/${account.id}`) }

  async function loadAccounts() {
    await fetchAccountsList(api)
  }

  function openCategory(category: Category) { navigate(`/categories/${category.id}`) }

  async function refreshCategories() {
    await fetchCategoriesList(api)
  }

  function onCategoryDeleted() {
    navigate('/categories')
    refreshCategories()
  }

  function openList(list: ParticipantListSummary) { navigate(`/participants/${list.id}`) }

  async function loadSelectedList(id: string) {
    selectedList = await api.fetchParticipantList(id)
  }

  async function refreshSelectedList() {
    if (selectedListId) await loadSelectedList(selectedListId)
  }

  async function refreshParticipantLists() {
    await fetchParticipantListsList(api)
  }

  // ParticipantListDetailView mutates settings/members of the currently open list, so refresh both
  // the full detail (shown on screen) and the lightweight summary (name/counts shown elsewhere).
  async function onParticipantListChanged() {
    await Promise.all([refreshSelectedList(), refreshParticipantLists()])
  }

  function onParticipantListDeleted() {
    navigate('/participants')
    refreshParticipantLists()
  }

  function openMember(memberId: string) {
    if (selectedListId) navigate(`/participants/${selectedListId}/members/${memberId}`)
  }

  function addMember() {
    if (selectedListId) navigate(`/participants/${selectedListId}/members/new`)
  }

  function onMemberSaved() {
    if (selectedListId) navigate(`/participants/${selectedListId}`)
    refreshParticipantLists()
  }

  function openTemplate(template: MatchTemplate) { navigate(`/templates/${template.id}`) }

  async function refreshTemplates() {
    await fetchTemplatesList(api)
  }

  function onTemplateDeleted() {
    navigate('/templates')
    refreshTemplates()
  }

  async function refreshPersonalBestStatus() {
    await fetchPersonalBestStatus(api)
  }

  function navigate(path: string, replace = false) {
    const route = navigateTo(path, replace)
    applyRouteResult(route)
  }

  function applyRoute() {
    applyRouteResult(resolveRoute())
  }

  function applyRouteResult(route: ReturnType<typeof resolveRoute>) {
    if (route.invalid) { navigate('/', true); return }
    view = route.view
    narrowcastScope = route.view === 'narrowcast' ? route.scope ?? null : null
    selectedResultsScope = route.view === 'match-results-scope' ? route.scope ?? null : null
    const matchScopedViews: View[] = ['match', 'match-metadata', 'match-devices', 'match-qr', 'match-results-scope', 'match-participant']
    selectedMatchId = matchScopedViews.includes(route.view) ? route.matchId ?? null : null
    if (selectedMatchId) loadSelectedMatch(selectedMatchId)
    else { selectedMatch = null; matchSourceList = null }
    const competitionScopedViews: View[] = ['competition', 'competition-results']
    selectedCompetitionId = competitionScopedViews.includes(route.view) ? route.competitionId ?? null : null
    if (selectedCompetitionId) loadSelectedCompetition(selectedCompetitionId)
    else selectedCompetition = null
    selectedParticipantId = route.view === 'match-participant' ? route.participantId ?? null : null
    selectedTenantId = route.view === 'tenant' ? route.tenantId ?? null : null
    if (route.view === 'tenants') loadChildTenants()
    selectedAccountId = route.view === 'account' ? route.accountId ?? null : null
    if (route.view === 'accounts') loadAccounts()
    selectedCategoryId = route.view === 'category' ? route.categoryId ?? null : null
    selectedListId = route.view === 'participant-list' || route.view === 'participant' ? route.listId ?? null : null
    if (selectedListId) loadSelectedList(selectedListId)
    else selectedList = null
    selectedMemberId = route.view === 'participant' ? route.memberId ?? null : null
    selectedTemplateId = route.view === 'template' ? route.templateId ?? null : null
  }

  onMount(() => {
    const handlePopState = () => applyRoute()
    window.addEventListener('popstate', handlePopState)
    applyRoute()
    if (view !== 'narrowcast') {
      loadTenants()
      if (loggedIn) loadData()
      else if (location.pathname !== '/login') history.replaceState({}, '', '/login')
    }
    return () => window.removeEventListener('popstate', handlePopState)
  })
</script>

{#if view === 'narrowcast' && narrowcastScope}
  <LiveScoringView scope={narrowcastScope} />
{:else if view === 'match-qr' && selectedMatch}
  <MatchQrCodesView match={selectedMatch} tenantId={tenant} {language} labels={t} />
{:else if view === 'match-results-scope' && selectedMatchId && selectedResultsScope}
  <MatchResultsScopeView {api} matchId={selectedMatchId} scope={selectedResultsScope} {language} labels={t} />
{:else if view === 'competition-results' && selectedCompetitionId}
  <CompetitionResultsView {api} competitionId={selectedCompetitionId} tenantLogoUrl={$currentTenant?.logoUrl} {language} labels={t} />
{:else if !loggedIn}
  <LoginView bind:tenant tenants={$tenants} {tenantsLoading} {tenantsError} bind:username bind:password {loginError} {language} labels={t} onSubmit={signIn} onLanguageChange={setLanguage} />
{:else}
  <div class="app-shell">
    <AppHeader username={headerUsername} {language} {view} labels={t} tenantName={$currentTenant?.name} tenantLogoUrl={$currentTenant?.logoUrl} onNavigate={navigate} onLanguageChange={setLanguage} onLogout={signOut} />
    <main class="content">
      {#if view === 'home'}
        <HomeView matches={$matches} competitions={$competitions} {language} labels={t} quickLinks={homeQuickLinks} onOpenMatch={openMatch} onNavigate={navigate} onDeactivateAll={deactivateAllMatches} />
      {:else if view === 'matches'}
        <MatchesView {api} matches={$matches} templates={$templates} {language} labels={t} onOpenMatch={openMatch} onChanged={loadMatchesList} />
      {:else if view === 'match' && selectedMatch}
        {@const currentMatch = selectedMatch}
        <MatchDetailView {api} match={currentMatch} categories={$categories} sourceList={matchSourceList} {language} labels={t} onBack={() => navigate('/matches')} onToggleOpen={toggleSelectedMatch} onChanged={refreshSelectedMatch} onDeleted={onMatchDeleted} onEditMetadata={() => navigate(`/matches/${currentMatch.id}/edit`)} onManageDevices={() => navigate(`/matches/${currentMatch.id}/devices`)} onOpenParticipant={openParticipant} />
      {:else if view === 'match-metadata' && selectedMatch}
        {@const currentMatch = selectedMatch}
        <MatchMetadataEditView {api} match={currentMatch} categories={$categories} participantLists={$participantLists} labels={t} onBack={() => navigate(`/matches/${currentMatch.id}`)} onSaved={() => navigate(`/matches/${currentMatch.id}`)} onDeleted={onMatchDeleted} />
      {:else if view === 'match-devices' && selectedMatch}
        {@const currentMatch = selectedMatch}
        <MatchDevicesView {api} match={currentMatch} categories={$categories} labels={t} onBack={() => navigate(`/matches/${currentMatch.id}`)} onChanged={refreshSelectedMatch} />
      {:else if view === 'match-participant' && selectedMatch && selectedParticipant}
        {@const currentMatch = selectedMatch}
        <MatchParticipantView {api} match={currentMatch} participant={selectedParticipant} categories={$categories} sourceList={matchSourceList} labels={t} onBack={returnToSelectedMatch} onChanged={refreshSelectedMatch} onRemoved={() => navigate(`/matches/${currentMatch.id}`)} />
      {:else if view === 'competitions'}
        <CompetitionsView {api} competitions={$competitions} {language} labels={t} onOpenCompetition={openCompetition} onChanged={loadCompetitionsList} />
      {:else if view === 'competition' && selectedCompetition}
        {@const currentCompetition = selectedCompetition}
        <CompetitionDetailView {api} competition={currentCompetition} categories={$categories} matches={$matches} {language} labels={t} onBack={() => navigate('/competitions')} onChanged={refreshSelectedCompetition} onDeleted={onCompetitionDeleted} onViewResults={() => window.open(`/competitions/${currentCompetition.id}/results`, '_blank')} onCopied={(copy) => { loadCompetitionsList(); openCompetition(copy) }} />
      {:else if view === 'profile'}
        <ProfileView {api} labels={t} onBack={() => navigate('/')} />
      {:else if view === 'tenants' && isAdmin}
        <TenantsView tenants={$childTenants} labels={t} onOpenTenant={openTenant} onCreateTenant={createChildTenant} onBack={() => navigate('/')} />
      {:else if view === 'tenant' && selectedTenantId && isAdmin}
        <TenantEditView {api} tenantId={selectedTenantId} labels={t} onBack={() => navigate('/tenants')} onDeleted={onTenantDeleted} />
      {:else if view === 'accounts' && isAdmin}
        <AccountsView {api} accounts={$accounts} labels={t} onOpenAccount={openAccount} onBack={() => navigate('/')} />
      {:else if view === 'account' && selectedAccountId && isAdmin}
        <AccountEditView {api} accountId={selectedAccountId} currentAccountId={$profile?.id ?? null} labels={t} onBack={() => navigate('/accounts')} />
      {:else if view === 'categories'}
        <CategoriesView {api} categories={$categories} labels={t} onOpenCategory={openCategory} onChanged={refreshCategories} onBack={() => navigate('/')} />
      {:else if view === 'category' && selectedCategory}
        <CategoryDetailView {api} category={selectedCategory} labels={t} onChanged={refreshCategories} onDeleted={onCategoryDeleted} onBack={() => navigate('/categories')} />
      {:else if view === 'participants'}
        <ParticipantListsView {api} lists={$participantLists} labels={t} onOpenList={openList} onChanged={refreshParticipantLists} onBack={() => navigate('/')} />
      {:else if view === 'participant-list' && selectedList}
        <ParticipantListDetailView {api} list={selectedList} categories={$categories} {language} {canManage} labels={t} onOpenMember={openMember} onAddMember={addMember} onChanged={onParticipantListChanged} onDeleted={onParticipantListDeleted} onBack={() => navigate('/participants')} />
      {:else if view === 'participant' && selectedListId}
        <ParticipantMemberView {api} listId={selectedListId} member={selectedMember} categories={$categories} labels={t} onBack={() => navigate(`/participants/${selectedListId}`)} onSaved={onMemberSaved} onDeleted={onMemberSaved} />
      {:else if view === 'templates'}
        <TemplatesView {api} templates={$templates} participantLists={$participantLists} labels={t} onOpenTemplate={openTemplate} onChanged={refreshTemplates} onBack={() => navigate('/')} />
      {:else if view === 'template' && selectedTemplate}
        <TemplateEditView {api} template={selectedTemplate} categories={$categories} participantLists={$participantLists} labels={t} onBack={() => navigate('/templates')} onSaved={refreshTemplates} onDeleted={onTemplateDeleted} />
      {:else if view === 'personal-best'}
        <PersonalBestView {api} status={$personalBestStatus} labels={t} onBack={() => navigate('/')} onNavigate={navigate} onChanged={refreshPersonalBestStatus} />
      {:else if view === 'personal-best-classifiers'}
        <PersonalBestClassifiersView {api} labels={t} onBack={() => navigate('/personal-best')} />
      {:else if view === 'personal-best-disciplines'}
        <PersonalBestDisciplinesView {api} labels={t} onBack={() => navigate('/personal-best')} />
      {:else if view === 'personal-best-export-config'}
        <PersonalBestExportConfigView {api} labels={t} onBack={() => navigate('/personal-best')} />
      {:else if view === 'personal-best-import-config'}
        <PersonalBestImportConfigView {api} labels={t} onBack={() => navigate('/personal-best')} />
      {:else if view === 'personal-best-log'}
        <PersonalBestLogView {api} {language} labels={t} onBack={() => navigate('/personal-best')} />
      {/if}
      {#if loading}<p class="loading">{t.loadingTenantData}</p>{/if}
    </main>
  </div>
{/if}
