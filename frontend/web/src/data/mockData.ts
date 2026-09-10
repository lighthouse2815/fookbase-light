// ============================================================
// HackerNet — Mock Data
// ============================================================

export interface User {
  id: string;
  handle: string;       // @n3ur0hack
  displayName: string;  // N3UR0HACK
  avatar: string;       // initials
  avatarColor: string;  // background color for avatar
  bio: string;
  location: string;
  followers: number;
  following: number;
  posts: number;
  isOnline: boolean;
  badges: BadgeType[];
  joinDate: string;
}

export type BadgeType = 'root' | 'anon' | 'cyborg' | 'neural' | 'ghost';

export interface Post {
  id: string;
  authorId: string;
  content: string;
  timestamp: Date;
  likes: number;
  reposts: number;
  comments: number;
  tags: string[];
  codeSnippet?: { code: string; lang: string };
  isLiked: boolean;
  isReposted: boolean;
}

export interface Comment {
  id: string;
  postId: string;
  authorId: string;
  content: string;
  timestamp: Date;
  likes: number;
  isLiked?: boolean;
}

export interface Message {
  id: string;
  senderId: string;
  content: string;
  timestamp: Date;
  isEncrypted?: boolean;
  isRead?: boolean;
}

export interface Conversation {
  id: string;
  participantId: string;
  messages: Message[];
  lastMessageTime: Date;
}

export interface TrendingTopic {
  id: string;
  tag: string;
  posts: number;
  trend: 'up' | 'down' | 'hot';
}

export type NotificationType = 'like' | 'comment' | 'mention' | 'friend' | 'security' | 'group';

export interface Notification {
  id: string;
  userId: string;
  type: NotificationType;
  title: string;
  content?: string;
  timestamp: Date;
  isRead: boolean;
  targetUrl?: string;
  actionData?: {
    friendRequestStatus?: 'pending' | 'accepted' | 'declined';
  };
}

// -----------------------------------------------------------
// USERS
// -----------------------------------------------------------
export const USERS: User[] = [
  {
    id: 'u1',
    handle: 'n3ur0hack',
    displayName: 'N3UR0HACK',
    avatar: 'NH',
    avatarColor: 'avatar-grad-1',
    bio: 'kernel exploit dev | zero-day researcher | coffee addict\n"root or nothing"',
    location: 'San Francisco, CA',
    followers: 18420,
    following: 312,
    posts: 2841,
    isOnline: true,
    badges: ['root', 'neural'],
    joinDate: '2019.03.14',
  },
  {
    id: 'u2',
    handle: 'ph4nt0m_byte',
    displayName: 'PH4NT0M_BYTE',
    avatar: 'PB',
    avatarColor: 'avatar-grad-6',
    bio: 'malware analyst | reverse engineer\nghidra > ida (fight me)',
    location: 'Remote',
    followers: 9210,
    following: 88,
    posts: 1337,
    isOnline: true,
    badges: ['anon', 'ghost'],
    joinDate: '2020.10.31',
  },
  {
    id: 'u3',
    handle: 'crypt0_viper',
    displayName: 'CRYPT0_VIPER',
    avatar: 'CV',
    avatarColor: 'avatar-grad-2',
    bio: 'blockchain security | smart contract auditor',
    location: 'Singapore',
    followers: 6744,
    following: 201,
    posts: 987,
    isOnline: false,
    badges: ['cyborg'],
    joinDate: '2021.01.01',
  },
  {
    id: 'u4',
    handle: 'syn_fl00d',
    displayName: 'SYN_FL00D',
    avatar: 'SF',
    avatarColor: 'avatar-grad-4',
    bio: 'network sec | packet whisperer',
    location: 'Berlin, DE',
    followers: 4120,
    following: 445,
    posts: 521,
    isOnline: true,
    badges: ['anon'],
    joinDate: '2022.04.20',
  },
  {
    id: 'u5',
    handle: 'b1n4ry_ghost',
    displayName: 'B1N4RY_GHOST',
    avatar: 'BG',
    avatarColor: 'avatar-grad-5',
    bio: 'CTF player | pwn specialist\nsegfault = skill issue (skill = mine)',
    location: 'Tokyo, JP',
    followers: 12300,
    following: 99,
    posts: 1590,
    isOnline: false,
    badges: ['ghost', 'root'],
    joinDate: '2018.08.08',
  },
  {
    id: 'u6',
    handle: 'z3r0_d4y',
    displayName: 'Z3R0_D4Y',
    avatar: 'ZD',
    avatarColor: 'avatar-grad-3',
    bio: 'bug bounty hunter | pentest | OSCP certified\n"if it connects, it\'s mine"',
    location: 'London, UK',
    followers: 21000,
    following: 156,
    posts: 3312,
    isOnline: true,
    badges: ['root', 'neural', 'ghost'],
    joinDate: '2017.07.17',
  },
];

// Current logged-in user
export const CURRENT_USER = USERS[0];

// -----------------------------------------------------------
// POSTS
// -----------------------------------------------------------
export const POSTS: Post[] = [
  {
    id: 'p1',
    authorId: 'u2',
    content:
      'Found a UAF vuln in a popular browser engine. Spent 3 weeks writing a PoC. The patch took devs 4 hours. This is why I drink.',
    timestamp: new Date(Date.now() - 1000 * 60 * 8),
    likes: 842,
    reposts: 201,
    comments: 77,
    tags: ['#vuln', '#browser', '#uaf'],
    isLiked: false,
    isReposted: false,
  },
  {
    id: 'p2',
    authorId: 'u6',
    content:
      'PSA: Stop using MD5 for anything. I mean anything. I can crack your MD5 password hash before I finish this sentence.',
    timestamp: new Date(Date.now() - 1000 * 60 * 23),
    likes: 1337,
    reposts: 512,
    comments: 128,
    tags: ['#crypto', '#md5', '#opsec'],
    codeSnippet: {
      lang: 'bash',
      code: `$ hashcat -a 0 -m 0 hash.txt rockyou.txt
Recovered: 1/1 (100.00%) Digests
Speed: 18928.4 MH/s`,
    },
    isLiked: true,
    isReposted: false,
  },
  {
    id: 'p3',
    authorId: 'u5',
    content:
      'New CTF writeup is up. 48h of pain condensed into 12min read. ROP chains, heap grooming, and one very satisfying shell.\n\nTag someone who needs to learn pwntools.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2),
    likes: 621,
    reposts: 188,
    comments: 43,
    tags: ['#ctf', '#pwn', '#rop', '#writeup'],
    isLiked: false,
    isReposted: false,
  },
  {
    id: 'p4',
    authorId: 'u3',
    content:
      'Audited another DeFi protocol. Another reentrancy bug. Another $40M at risk. Another project that didn\'t read the Ethereum docs. Why is history allergic to repetition?',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 5),
    likes: 2100,
    reposts: 890,
    comments: 312,
    tags: ['#web3', '#solidity', '#audit', '#defi'],
    codeSnippet: {
      lang: 'solidity',
      code: `// 🔴 VULNERABLE — Classic reentrancy
function withdraw(uint amount) external {
  require(balances[msg.sender] >= amount);
  (bool ok,) = msg.sender.call{value: amount}(""); // ← HERE
  require(ok);
  balances[msg.sender] -= amount; // too late
}`,
    },
    isLiked: true,
    isReposted: true,
  },
  {
    id: 'p5',
    authorId: 'u4',
    content:
      'TCP handshake goes brrr 🤝\nJust wrote a raw socket scanner in Go. 65535 ports in under 2 seconds on LAN. Wireshark is having a seizure.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 8),
    likes: 445,
    reposts: 111,
    comments: 29,
    tags: ['#golang', '#network', '#scanner'],
    codeSnippet: {
      lang: 'go',
      code: `func scanPort(host string, port int) bool {
  addr := fmt.Sprintf("%s:%d", host, port)
  conn, err := net.DialTimeout("tcp", addr, 200*time.Millisecond)
  if err != nil { return false }
  conn.Close()
  return true
}`,
    },
    isLiked: false,
    isReposted: false,
  },
  {
    id: 'p6',
    authorId: 'u6',
    content:
      'Hot take: The best firewall rule is "deny all, permit none." Change my mind.\n\nAlso shoutout to every sysadmin who left RDP exposed to 0.0.0.0 — you\'re keeping me employed.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 12),
    likes: 3200,
    reposts: 1100,
    comments: 287,
    tags: ['#pentest', '#rdp', '#firewall', '#hottake'],
    isLiked: false,
    isReposted: false,
  },
  {
    id: 'p7',
    authorId: 'u2',
    content:
      'Week 3 of reversing this obfuscated binary. I\'m starting to understand what the developer was thinking. It wasn\'t pleasant.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24),
    likes: 722,
    reposts: 98,
    comments: 55,
    tags: ['#reverseng', '#malware', '#ghidra'],
    isLiked: false,
    isReposted: false,
  },
];

// -----------------------------------------------------------
// COMMENTS
// -----------------------------------------------------------
export const INITIAL_COMMENTS: Comment[] = [
  {
    id: 'cm1',
    postId: 'p1',
    authorId: 'u6',
    content: 'Classic. 3 weeks of sleepless nights vs 4 lines of C++ patch. Hope you grabbed that CVE credit!',
    timestamp: new Date(Date.now() - 1000 * 60 * 6),
    likes: 24,
    isLiked: false,
  },
  {
    id: 'cm2',
    postId: 'p1',
    authorId: 'u1',
    content: 'Did you trigger it through GC pressure or canvas manipulation? Asking for a friend 😉',
    timestamp: new Date(Date.now() - 1000 * 60 * 3),
    likes: 12,
    isLiked: true,
  },
  {
    id: 'cm3',
    postId: 'p2',
    authorId: 'u3',
    content: 'Tell that to half the government systems still using NTLMv1 lmao.',
    timestamp: new Date(Date.now() - 1000 * 60 * 20),
    likes: 45,
    isLiked: true,
  },
  {
    id: 'cm4',
    postId: 'p2',
    authorId: 'u5',
    content: 'My 4090 does 95 GH/s on NTLM. MD5 is literally plaintext at this point.',
    timestamp: new Date(Date.now() - 1000 * 60 * 15),
    likes: 31,
    isLiked: false,
  },
  {
    id: 'cm5',
    postId: 'p3',
    authorId: 'u4',
    content: 'ROP chains make my brain hurt. Bookmarked for the weekend, great writeup as always!',
    timestamp: new Date(Date.now() - 1000 * 60 * 55),
    likes: 8,
    isLiked: false,
  },
  {
    id: 'cm6',
    postId: 'p4',
    authorId: 'u1',
    content: 'Checks-Effects-Interactions pattern is like 3 lines of code. Why is it so hard for devs to follow?',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3),
    likes: 89,
    isLiked: true,
  },
  {
    id: 'cm7',
    postId: 'p4',
    authorId: 'u6',
    content: 'Auditing DeFi is free money. Just Ctrl+F for external calls before state mutations 💀',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2),
    likes: 64,
    isLiked: false,
  },
  {
    id: 'cm8',
    postId: 'p5',
    authorId: 'u2',
    content: 'SYN scanner or full connect? Either way, sysadmins on your subnet are crying right now.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 6),
    likes: 15,
    isLiked: false,
  },
  {
    id: 'cm9',
    postId: 'p6',
    authorId: 'u5',
    content: 'Port 3389 open to 0.0.0.0 is basically a honeypot at this point.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 10),
    likes: 73,
    isLiked: true,
  },
  {
    id: 'cm10',
    postId: 'p7',
    authorId: 'u6',
    content: 'VMP or Themida? If it\'s custom control-flow flattening, may god have mercy on your soul.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 18),
    likes: 19,
    isLiked: false,
  },
];

export function getCommentsByPostId(postId: string): Comment[] {
  return INITIAL_COMMENTS.filter((c) => c.postId === postId);
}
// -----------------------------------------------------------
export const CONVERSATIONS: Conversation[] = [
  {
    id: 'c1',
    participantId: 'u6',
    lastMessageTime: new Date(Date.now() - 1000 * 60 * 5),
    messages: [
      {
        id: 'm1',
        senderId: 'u6',
        content: 'yo, saw your post on the UAF bug. got a question about your PoC setup',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2),
        isRead: true,
      },
      {
        id: 'm2',
        senderId: 'u1',
        content: 'sure, what\'s up?',
        timestamp: new Date(Date.now() - 1000 * 60 * 90),
        isRead: true,
      },
      {
        id: 'm3',
        senderId: 'u6',
        content: 'did you need to disable ASLR or was the spray reliable enough without it?',
        timestamp: new Date(Date.now() - 1000 * 60 * 88),
        isRead: true,
      },
      {
        id: 'm4',
        senderId: 'u1',
        content: 'needed to massage the heap a bit. 94% reliable on target config. ASLR was a pain but not the real blocker',
        timestamp: new Date(Date.now() - 1000 * 60 * 60),
        isRead: true,
      },
      {
        id: 'm5',
        senderId: 'u6',
        content: '[ENCRYPTED_MSG:3a7f...] — decryption key exchanged via Signal',
        timestamp: new Date(Date.now() - 1000 * 60 * 5),
        isEncrypted: true,
        isRead: false,
      },
    ],
  },
  {
    id: 'c2',
    participantId: 'u5',
    lastMessageTime: new Date(Date.now() - 1000 * 60 * 45),
    messages: [
      {
        id: 'm6',
        senderId: 'u5',
        content: 'gg on the H1 bounty. what was the severity in the end?',
        timestamp: new Date(Date.now() - 1000 * 60 * 90),
        isRead: true,
      },
      {
        id: 'm7',
        senderId: 'u1',
        content: 'P1, $15k. not bad for a weekend',
        timestamp: new Date(Date.now() - 1000 * 60 * 60),
        isRead: true,
      },
      {
        id: 'm8',
        senderId: 'u5',
        content: 'legend. buy me a coffee sometime lol',
        timestamp: new Date(Date.now() - 1000 * 60 * 45),
        isRead: false,
      },
    ],
  },
  {
    id: 'c3',
    participantId: 'u3',
    lastMessageTime: new Date(Date.now() - 1000 * 60 * 60 * 3),
    messages: [
      {
        id: 'm9',
        senderId: 'u3',
        content: 'reviewing that Solidity contract you DMed. found 2 more issues. this is... not good',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 4),
        isRead: true,
      },
      {
        id: 'm10',
        senderId: 'u1',
        content: 'how bad?',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3.5),
        isRead: true,
      },
      {
        id: 'm11',
        senderId: 'u3',
        content: '[ENCRYPTED_MSG:9b2c...] — critical findings attached',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 3),
        isEncrypted: true,
        isRead: true,
      },
    ],
  },
  {
    id: 'c4',
    participantId: 'u4',
    lastMessageTime: new Date(Date.now() - 1000 * 60 * 60 * 8),
    messages: [
      {
        id: 'm12',
        senderId: 'u4',
        content: 'bro your port scanner post is being reposted everywhere. you famous now',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 9),
        isRead: true,
      },
      {
        id: 'm13',
        senderId: 'u1',
        content: 'haha nice. it\'s really just goroutines + raw TCP, nothing fancy',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 8.5),
        isRead: true,
      },
      {
        id: 'm14',
        senderId: 'u4',
        content: 'can you send me the full source? wanna add banner grabbing',
        timestamp: new Date(Date.now() - 1000 * 60 * 60 * 8),
        isRead: true,
      },
    ],
  },
];

// -----------------------------------------------------------
// NOTIFICATIONS
// -----------------------------------------------------------
export const INITIAL_NOTIFICATIONS: Notification[] = [
  {
    id: 'notif-1',
    userId: 'u6',
    type: 'like',
    title: 'đã thích bài viết của bạn về UAF vulnerability.',
    content: '"Found a UAF vuln in a popular browser engine. Spent 3 weeks..."',
    timestamp: new Date(Date.now() - 1000 * 60 * 5),
    isRead: false,
    targetUrl: '/feed',
  },
  {
    id: 'notif-2',
    userId: 'u2',
    type: 'comment',
    title: 'đã bình luận về bài viết của bạn:',
    content: '"SYN scanner or full connect? Either way, sysadmins on your subnet..."',
    timestamp: new Date(Date.now() - 1000 * 60 * 25),
    isRead: false,
    targetUrl: '/feed',
  },
  {
    id: 'notif-3',
    userId: 'u5',
    type: 'friend',
    title: 'đã gửi cho bạn lời mời kết bạn.',
    content: 'CTF player | pwn specialist',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 1.5),
    isRead: false,
    targetUrl: '/profile',
    actionData: {
      friendRequestStatus: 'pending',
    },
  },
  {
    id: 'notif-4',
    userId: 'u3',
    type: 'mention',
    title: 'đã nhắc đến bạn trong một bình luận:',
    content: '"@n3ur0hack check out this reentrancy bug in the latest DeFi audit."',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 4),
    isRead: true,
    targetUrl: '/feed',
  },
  {
    id: 'notif-5',
    userId: 'u1',
    type: 'security',
    title: 'Cảnh báo bảo mật hệ thống',
    content: 'Phát hiện phiên đăng nhập mới từ IP 192.168.1.105 (San Francisco, CA).',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24),
    isRead: true,
  },
  {
    id: 'notif-6',
    userId: 'u4',
    type: 'group',
    title: 'đã chia sẻ bài viết của bạn vào nhóm Hack Kingdom Club.',
    content: '"TCP handshake goes brrr 🤝 Just wrote a raw socket scanner..."',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 48),
    isRead: true,
    targetUrl: '/feed',
  },
];

// -----------------------------------------------------------
// TRENDING
// -----------------------------------------------------------
export const TRENDING_TOPICS: TrendingTopic[] = [
  { id: 't1', tag: '#0day', posts: 18420, trend: 'hot' },
  { id: 't2', tag: '#log4shell', posts: 12100, trend: 'up' },
  { id: 't3', tag: '#ctf', posts: 9880, trend: 'up' },
  { id: 't4', tag: '#opsec', posts: 7720, trend: 'up' },
  { id: 't5', tag: '#reverseng', posts: 6540, trend: 'up' },
  { id: 't6', tag: '#defi', posts: 5910, trend: 'hot' },
  { id: 't7', tag: '#rootkit', posts: 4200, trend: 'up' },
  { id: 't8', tag: '#pwn', posts: 3980, trend: 'up' },
  { id: 't9', tag: '#bounty', posts: 3440, trend: 'up' },
  { id: 't10', tag: '#ghidra', posts: 2810, trend: 'down' },
  { id: 't11', tag: '#network', posts: 2540, trend: 'up' },
  { id: 't12', tag: '#solidity', posts: 2220, trend: 'hot' },
];

// -----------------------------------------------------------
// HELPERS
// -----------------------------------------------------------
export function getUserById(id: string): User | undefined {
  return USERS.find((u) => u.id === id);
}

export function formatNumber(n: number): string {
  if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
  if (n >= 1_000) return (n / 1_000).toFixed(1) + 'K';
  return n.toString();
}

export function formatTimestamp(date: Date): string {
  const diff = Date.now() - date.getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h`;
  const days = Math.floor(hrs / 24);
  return `${days}d`;
}
