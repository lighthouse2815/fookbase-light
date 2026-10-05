import { useAuth } from '../auth/AuthProvider';
import { SearchScreen } from '../features/search/SearchScreen';

export default function Search() {
  const { session } = useAuth();
  return <SearchScreen key={session?.user.id ?? 'signed-out'} accountId={session?.user.id} />;
}
