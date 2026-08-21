<script lang="ts">
  import { onMount } from 'svelte'
  import { ApiClient, fetchTenants, login } from './lib/api'
  import { labelForError } from './lib/errors'
  import { translationsFor } from './lib/i18n'
  import { managementItems } from './lib/managementData'
  import { navigateTo, resolveRoute } from './lib/router'
  import type { Account, Category, Competition, Language, Match, NamedItem, ParticipantList, Profile, Tenant, View } from './lib/types'
  import { isManagementView } from './lib/types'
  import AccountEditView from './lib/AccountEditView.svelte'
  import AccountsView from './lib/AccountsView.svelte'
  import AppHeader from './lib/AppHeader.svelte'
  import CategoriesView from './lib/CategoriesView.svelte'
  import CategoryDetailView from './lib/CategoryDetailView.svelte'
  import CompetitionsView from './lib/CompetitionsView.svelte'
  import HomeView from './lib/HomeView.svelte'
  import LoginView from './lib/LoginView.svelte'
  import ManagementView from './lib/ManagementView.svelte'
  import MatchDetailView from './lib/MatchDetailView.svelte'
  import MatchesView from './lib/MatchesView.svelte'
  import ParticipantListDetailView from './lib/ParticipantListDetailView.svelte'
  import ParticipantListsView from './lib/ParticipantListsView.svelte'
  import ParticipantMemberView from './lib/ParticipantMemberView.svelte'
  import ProfileView from './lib/ProfileView.svelte'
  import TenantEditView from './lib/TenantEditView.svelte'
  import TenantsView from './lib/TenantsView.svelte'

  const rootTenant = '00000000-0000-0000-0000-000000000001'
  let loggedIn = localStorage.getItem('centaur-token') !== null
  let token = localStorage.getItem('centaur-token') ?? ''
  let tenant = localStorage.getItem('centaur-tenant') ?? rootTenant
  let tenants: Tenant[] = []
  let tenantsLoading = false
  let tenantsError = ''
  let username = ''
  let password = ''
  let language = (localStorage.getItem('centaur-language') ?? 'en') as Language
  let view: View = 'home'
  let matches: Match[] = []
  let competitions: Competition[] = []
  let selectedMatch: Match | null = null
  let loginError = ''
  let loading = false
  let profile: Profile | null = null
  let currentTenant: Tenant | null = null
  let childTenants: Tenant[] = []
  let selectedTenantId: string | null = null
  let accounts: Account[] = []
  let selectedAccountId: string | null = null
  let categories: Category[] = []
  let selectedCategoryId: string | null = null
  let participantLists: ParticipantList[] = []
  let selectedListId: string | null = null
  let selectedMemberId: string | null = null

  $: t = translationsFor(language)
  $: managementView = isManagementView(view) ? view : null
  $: namedItems = managementView ? managementItems(managementView) : ([] as NamedItem[])
  $: isAdmin = profile?.authorization === 'Administrator'
  $: homeQuickLinks = (
    [['participants', t.participants], ['categories', t.categories], ['templates', t.templates]] as [string, string][]
  ).concat(isAdmin ? [['accounts', t.accounts], ['tenants', t.tenants]] : [])
  $: selectedCategory = categories.find((category) => category.id === selectedCategoryId) ?? null
  $: selectedList = participantLists.find((list) => list.id === selectedListId) ?? null
  $: selectedMember = selectedMemberId && selectedMemberId !== 'new' ? selectedList?.members.find((member) => member.id === selectedMemberId) ?? null : null

  const api = new ApiClient(() => token, signOut)

  async function loadTenants() {
    tenantsLoading = true
    tenantsError = ''
    try {
      tenants = await fetchTenants()
      if (tenants.length > 0 && !tenants.some((item) => item.id === tenant)) tenant = tenants[0].id
    } catch {
      tenants = []
      tenantsError = t.tenantsLoadError
    } finally { tenantsLoading = false }
  }

  async function loadData() {
    if (!token) return
    loading = true
    try {
      matches = await api.fetchMatches()
      competitions = await api.fetchCompetitions()
      profile = await api.fetchProfile()
      currentTenant = await api.fetchCurrentTenant()
      categories = await api.fetchCategories()
      participantLists = await api.fetchParticipantLists()
      applyRoute()
    } catch { matches = []; competitions = [] } finally { loading = false }
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
    localStorage.removeItem('centaur-token'); token = ''; loggedIn = false; username = ''; password = ''; loginError = ''; selectedMatch = null; navigate('/', true)
  }

  function setLanguage(value: string) { language = value as Language; localStorage.setItem('centaur-language', value) }

  function openMatch(match: Match) { selectedMatch = match; navigate(`/matches/${match.id}`) }

  function toggleSelectedMatch() {
    if (!selectedMatch) return
    api.updateMatch(selectedMatch.id, { ...selectedMatch, isOpen: !selectedMatch.isOpen, ends: 10, arrowsPerEnd: 3, allowFreeParticipants: false, keyboardJson: '[]', scoringRulesJson: '[]' }).then(loadData)
  }

  function deactivateAllMatches() {
    api.deactivateAllMatches().then(loadData)
  }

  function openTenant(childTenant: Tenant) { navigate(`/tenants/${childTenant.id}`) }

  async function loadChildTenants() {
    childTenants = await api.fetchChildTenants()
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
    accounts = await api.fetchAccounts()
  }

  function openCategory(category: Category) { navigate(`/categories/${category.id}`) }

  async function refreshCategories() {
    categories = await api.fetchCategories()
  }

  function onCategoryDeleted() {
    navigate('/categories')
    refreshCategories()
  }

  function openList(list: ParticipantList) { navigate(`/participants/${list.id}`) }

  async function refreshParticipantLists() {
    participantLists = await api.fetchParticipantLists()
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
    selectedMatch = route.view === 'match' ? matches.find((match) => match.id === route.matchId) ?? selectedMatch : null
    selectedTenantId = route.view === 'tenant' ? route.tenantId ?? null : null
    if (route.view === 'tenants') loadChildTenants()
    selectedAccountId = route.view === 'account' ? route.accountId ?? null : null
    if (route.view === 'accounts') loadAccounts()
    selectedCategoryId = route.view === 'category' ? route.categoryId ?? null : null
    selectedListId = route.view === 'participant-list' || route.view === 'participant' ? route.listId ?? null : null
    selectedMemberId = route.view === 'participant' ? route.memberId ?? null : null
  }

  onMount(() => {
    const handlePopState = () => applyRoute()
    window.addEventListener('popstate', handlePopState)
    applyRoute()
    loadTenants()
    if (loggedIn) loadData()
    return () => window.removeEventListener('popstate', handlePopState)
  })
</script>

{#if !loggedIn}
  <LoginView bind:tenant {tenants} {tenantsLoading} {tenantsError} bind:username bind:password {loginError} {language} labels={t} onSubmit={signIn} onLanguageChange={setLanguage} />
{:else}
  <div class="app-shell">
    <AppHeader {username} {language} {view} labels={t} tenantName={currentTenant?.name} tenantLogoUrl={currentTenant?.logoUrl} onNavigate={navigate} onLanguageChange={setLanguage} onLogout={signOut} />
    <main class="content">
      {#if view === 'home'}
        <HomeView {matches} {competitions} {language} labels={t} quickLinks={homeQuickLinks} onOpenMatch={openMatch} onNavigate={navigate} onDeactivateAll={deactivateAllMatches} />
      {:else if view === 'matches'}
        <MatchesView {matches} {language} labels={t} onOpenMatch={openMatch} />
      {:else if view === 'match' && selectedMatch}
        <MatchDetailView match={selectedMatch} {language} labels={t} onBack={() => navigate('/matches')} onToggleOpen={toggleSelectedMatch} />
      {:else if view === 'competitions'}
        <CompetitionsView {competitions} {language} labels={t} />
      {:else if view === 'profile'}
        <ProfileView {api} labels={t} onBack={() => navigate('/')} />
      {:else if view === 'tenants' && isAdmin}
        <TenantsView tenants={childTenants} labels={t} onOpenTenant={openTenant} onCreateTenant={createChildTenant} onBack={() => navigate('/')} />
      {:else if view === 'tenant' && selectedTenantId && isAdmin}
        <TenantEditView {api} tenantId={selectedTenantId} labels={t} onBack={() => navigate('/tenants')} onDeleted={onTenantDeleted} />
      {:else if view === 'accounts' && isAdmin}
        <AccountsView {api} {accounts} labels={t} onOpenAccount={openAccount} onBack={() => navigate('/')} />
      {:else if view === 'account' && selectedAccountId && isAdmin}
        <AccountEditView {api} accountId={selectedAccountId} currentAccountId={profile?.id ?? null} labels={t} onBack={() => navigate('/accounts')} />
      {:else if view === 'categories'}
        <CategoriesView {api} {categories} labels={t} onOpenCategory={openCategory} onChanged={refreshCategories} onBack={() => navigate('/')} />
      {:else if view === 'category' && selectedCategory}
        <CategoryDetailView {api} category={selectedCategory} labels={t} onChanged={refreshCategories} onDeleted={onCategoryDeleted} onBack={() => navigate('/categories')} />
      {:else if view === 'participants'}
        <ParticipantListsView {api} lists={participantLists} labels={t} onOpenList={openList} onChanged={refreshParticipantLists} onBack={() => navigate('/')} />
      {:else if view === 'participant-list' && selectedList}
        <ParticipantListDetailView {api} list={selectedList} {categories} labels={t} onOpenMember={openMember} onAddMember={addMember} onChanged={refreshParticipantLists} onBack={() => navigate('/participants')} />
      {:else if view === 'participant' && selectedListId}
        <ParticipantMemberView {api} listId={selectedListId} member={selectedMember} {categories} labels={t} onBack={() => navigate(`/participants/${selectedListId}`)} onSaved={onMemberSaved} onDeleted={onMemberSaved} />
      {:else if managementView}
        <section class="management-view">
          <button class="back-link" on:click={() => navigate('/')}>← {t.home}</button>
          <ManagementView eyebrow={t.eyebrowTenantConfig} title={t[managementView]} description={t.manageDataDescription} action={`+ ${t.add}`} items={namedItems} labels={t} />
        </section>
      {/if}
      {#if loading}<p class="loading">{t.loadingTenantData}</p>{/if}
    </main>
  </div>
{/if}
