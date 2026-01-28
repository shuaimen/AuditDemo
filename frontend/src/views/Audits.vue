<template>
  <div class="card">
    <div class="row">
      <div class="col">
        <label>年度</label>
        <input v-model.number="filterYear" type="number" />
      </div>
      <div class="col">
        <label>工厂</label>
        <select v-model="filterFactoryId">
          <option value="">全部</option>
          <option v-for="f in factories" :key="f.factoryId" :value="f.factoryId">{{ f.factoryCode }} - {{ f.name }}</option>
        </select>
      </div>
      <div class="col" style="display:flex;align-items:end;gap:10px">
        <button @click="load">查询</button>
        <button class="secondary" @click="openCreate">新建评鉴单</button>
      </div>
    </div>
  </div>

  <div class="card" v-if="creating">
    <h3 style="margin:0 0 10px 0">新建评鉴单</h3>
    <div class="row">
      <div class="col">
        <label>类型</label>
        <select v-model.number="create.auditType">
          <option :value="1">年度评鉴</option>
          <option :value="2">新引入</option>
        </select>
      </div>
      <div class="col">
        <label>年度</label>
        <input v-model.number="create.year" type="number" />
      </div>
      <div class="col">
        <label>工厂</label>
        <select v-model="create.factoryId">
          <option v-for="f in factories" :key="f.factoryId" :value="f.factoryId">{{ f.factoryCode }} - {{ f.name }}</option>
        </select>
      </div>
      <div class="col">
        <label>复制去年结果为今年默认值</label>
        <select v-model="create.copyLastYear">
          <option :value="true">是</option>
          <option :value="false">否</option>
        </select>
      </div>
    </div>
    <div style="margin-top:12px;display:flex;gap:10px;align-items:center">
      <button @click="doCreate">创建</button>
      <button class="secondary" @click="creating=false">取消</button>
      <span v-if="err" class="bad">{{ err }}</span>
    </div>
  </div>

  <div class="card">
    <h3 style="margin:0 0 10px 0">评鉴单列表</h3>
    <div v-if="loading">加载中...</div>
    <div v-else>
      <div v-if="items.length===0" style="color:#666">暂无数据</div>
      <div v-for="a in items" :key="a.auditId" class="card" style="background:#fbfbfe">
        <div class="row">
          <div class="col">
            <div><b>{{ a.factoryCode }}</b> {{ a.factoryName }}</div>
            <div style="color:#666;font-size:13px">{{ a.year }} / {{ a.auditType===1? '年度评鉴':'新引入' }}</div>
          </div>
          <div class="col">
            <span class="badge">{{ a.statusText }}</span>
            <span class="badge" style="margin-left:8px">等级：{{ a.finalGrade || '-' }}</span>
          </div>
          <div class="col" style="display:flex;align-items:center;justify-content:end">
            <router-link :to="'/audits/'+a.auditId">进入</router-link>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { http } from '../api/http'

const filterYear = ref(new Date().getFullYear())
const filterFactoryId = ref('')

const factories = ref([])
const items = ref([])
const loading = ref(false)
const creating = ref(false)
const err = ref('')

const create = ref({
  auditType: 1,
  year: new Date().getFullYear(),
  factoryId: '',
  copyLastYear: true
})

async function loadFactories() {
  const r = await http.get('/api/factories')
  factories.value = r.data.items
  if (!create.value.factoryId && factories.value.length) create.value.factoryId = factories.value[0].factoryId
}

async function load() {
  loading.value = true
  try {
    const params = {}
    if (filterYear.value) params.year = filterYear.value
    if (filterFactoryId.value) params.factoryId = filterFactoryId.value
    const r = await http.get('/api/audits', { params })
    items.value = r.data.items
  } finally {
    loading.value = false
  }
}

function openCreate() {
  err.value = ''
  creating.value = true
}

async function doCreate() {
  err.value = ''
  try {
    const r = await http.post('/api/audits', create.value)
    creating.value = false
    location.href = '/app/audits/' + r.data.auditId
  } catch (e) {
    err.value = e?.response?.data?.message || '创建失败'
  }
}

onMounted(async () => {
  await loadFactories()
  await load()
})
</script>
